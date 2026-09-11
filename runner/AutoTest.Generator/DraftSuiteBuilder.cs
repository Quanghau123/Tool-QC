using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoTest.Generator;

// Sinh bản nháp từ contract đã phân tích; mọi phần chưa đủ bằng chứng vẫn bị khóa bởi preflight.
public static class DraftSuiteBuilder
{
    public static GeneratedSuite Build(string project, GeneratorAnalysis analysis, ISet<string>? included)
    {
        EndpointContract[] endpoints = analysis.Endpoints.Where(endpoint => included is null || included.Count == 0 || included.Contains(endpoint.Id)).ToArray();
        List<object> cases = [];
        foreach (EndpointContract endpoint in endpoints)
        {
            cases.Add(BuildSuccessCase(endpoint, analysis.Types));
            foreach (ValidationContract validation in endpoint.Validations) cases.Add(BuildValidationCase(endpoint, validation, analysis.Types));
        }
        return new(2, project, true, true, cases);
    }

    private static object BuildSuccessCase(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types)
    {
        string stepName = $"Gọi thành công {endpoint.Method} {endpoint.Path}";
        return Case(endpoint, $"generated.{endpoint.Id}.success", $"Kiểm tra thành công {endpoint.Method} {endpoint.Path}", ["generated-draft", "success", Group(endpoint)], stepName, BuildPath(endpoint, types), BuildBody(endpoint, types), SuccessStatuses(endpoint), BuildResponseAssertions(endpoint, types), endpoint.Validations.Count > 0 ? Coverage("covered", [stepName], null) : Coverage("notApplicable", null, "Source không khai báo FluentValidation cho request này."));
    }

    private static object BuildValidationCase(EndpointContract endpoint, ValidationContract validation, IReadOnlyDictionary<string, TypeContract> types)
    {
        string stepName = $"Từ chối {validation.Property} vi phạm {validation.Rule}";
        Dictionary<string, object?>? body = BuildBody(endpoint, types);
        string path = BuildPath(endpoint, types);
        BoundaryValue invalid = InvalidValue(validation, FindProperty(endpoint, validation.Property, types));
        ParameterContract? requestParameter = endpoint.Parameters.FirstOrDefault(parameter => parameter.Source is "body" or "query");
        if (requestParameter?.Source == "query") path = SetQueryValue(path, validation.Property, invalid.Value);
        else if (body is not null) SetNested(body, validation.Property, invalid.Value);
        object validationCoverage = invalid.Resolved ? Coverage("covered", [stepName], null) : Coverage("blocked", null, $"Không resolve được boundary '{validation.Value}' từ source; generator không tự đoán giá trị lỗi.");
        return Case(endpoint, $"generated.{endpoint.Id}.validation.{Slug(validation.Property)}.{validation.Rule.ToLowerInvariant()}", $"Kiểm tra {validation.Property} vi phạm {validation.Rule}", ["generated-draft", "validation", Group(endpoint)], stepName, path, body, ErrorStatuses(endpoint), new Dictionary<string, object> { ["$.message"] = new { exists = true, type = "string" } }, validationCoverage);
    }

    private static object Case(EndpointContract endpoint, string id, string name, string[] tags, string stepName, string path, object? body, int[] statuses, Dictionary<string, object> assertions, object validationCoverage) => new
    {
        id, name, tags, destructive = endpoint.Method is not "GET" and not "HEAD", disabled = true, draft = true,
        variables = BuildVariables(endpoint),
        coverage = new
        {
            sourceVerification = new { files = new[] { endpoint.SourceFile }.Concat(endpoint.Validations.Select(rule => rule.SourceFile)).Distinct().ToArray(), verifiedAt = DateTimeOffset.Now.ToString("O"), contractVersion = "roslyn-source-scan-v3" },
            contract = Coverage("covered", [stepName], null), validation = validationCoverage,
            authentication = endpoint.RequiresAuthentication ? Coverage("blocked", null, "Cần chọn auth profile và bổ sung testcase 401/403 trước khi kích hoạt.") : Coverage("notApplicable", null, "Endpoint anonymous hoặc source không khai báo authorization."),
            scope = endpoint.Parameters.Any(parameter => parameter.Source == "route") ? Coverage("blocked", null, "Cần fixture khác scope để chứng minh không rò rỉ dữ liệu.") : Coverage("notApplicable", null, "Không phát hiện parent scope từ route."),
            integrity = endpoint.Method != "GET" ? Coverage("blocked", null, "Mutation phải có database hoặc side-effect assertion trước khi kích hoạt.") : Coverage("notApplicable", null, "Endpoint chỉ đọc."),
            lifecycle = endpoint.Branches.Count > 0 ? Coverage("blocked", null, $"Phát hiện {endpoint.Branches.Count} nhánh/guard; cần ánh xạ fixture và expected business result.") : Coverage("notApplicable", null, "Không phát hiện nhánh lifecycle trực tiếp trong action."),
            concurrency = Coverage("notApplicable", null, "Không phát hiện contract concurrency trực tiếp."), reporting = Coverage("covered", [stepName], null)
        },
        detectedContract = endpoint,
        steps = new[] { new { name = stepName, auth = endpoint.RequiresAuthentication ? "admin" : null, request = new { method = endpoint.Method, path, body }, expect = new { statusOneOf = statuses, json = assertions } } }
    };

    private static Dictionary<string, string> BuildVariables(EndpointContract endpoint) => endpoint.Parameters.Where(parameter => parameter.Source is "route" or "route-or-query").ToDictionary(parameter => parameter.Name, parameter => SampleScalar(parameter.Type, parameter.Name).ToString() ?? "${unique}", StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, object?>? BuildBody(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types) => endpoint.Parameters.FirstOrDefault(parameter => parameter.Source == "body") is { } body ? SampleObject(body.Type, types, [], 0) : null;

    private static string BuildPath(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types)
    {
        string path = Regex.Replace(endpoint.Path, "\\{(?<name>[^}:]+)(:[^}]+)?\\}", match => "${" + match.Groups["name"].Value + "}");
        List<string> query = [];
        foreach (ParameterContract parameter in endpoint.Parameters.Where(parameter => parameter.Source == "query"))
        {
            if (TryType(types, parameter.Type, out TypeContract? dto)) query.AddRange(AllProperties(dto, types).Where(property => property.Required || IsPaging(property.JsonName)).Take(8).Select(property => $"{Uri.EscapeDataString(property.JsonName)}={Uri.EscapeDataString(SampleScalar(property.Type, property.JsonName).ToString() ?? string.Empty)}"));
            else query.Add($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(SampleScalar(parameter.Type, parameter.Name).ToString() ?? string.Empty)}");
        }
        return query.Count == 0 ? path : path + (path.Contains('?') ? "&" : "?") + string.Join('&', query);
    }

    private static Dictionary<string, object?> SampleObject(string type, IReadOnlyDictionary<string, TypeContract> types, HashSet<string> stack, int depth)
    {
        string clean = TypeKey(type); if (depth > 4 || !stack.Add(clean) || !TryType(types, type, out TypeContract? contract)) return [];
        Dictionary<string, object?> result = [];
        foreach (PropertyContract property in AllProperties(contract, types)) result[property.JsonName] = property.IsCollection ? new[] { Sample(property.ItemType ?? "object", property.JsonName, types, stack, depth + 1) } : Sample(property.Type, property.JsonName, types, stack, depth + 1);
        stack.Remove(clean); return result;
    }

    private static object? Sample(string type, string name, IReadOnlyDictionary<string, TypeContract> types, HashSet<string> stack, int depth)
    {
        if (TryType(types, type, out TypeContract? contract)) return contract.Kind == "enum" ? contract.EnumValues.FirstOrDefault() ?? "0" : SampleObject(type, types, stack, depth);
        return SampleScalar(type, name);
    }

    private static object SampleScalar(string type, string name)
    {
        string clean = CleanType(type).ToLowerInvariant(); string lowerName = name.ToLowerInvariant();
        if (clean is "bool" or "boolean") return true;
        if (Regex.IsMatch(clean, "^(byte|short|int|int32|long|int64|float|double|decimal)$")) return lowerName is "pagesize" ? 10 : 1;
        if (clean is "datetime" or "datetimeoffset") return "${nowIso}";
        if (clean == "guid") return "${guid1}";
        if (lowerName.Contains("email")) return "generated.${unique}@integration.test";
        if (lowerName.Contains("password")) return "Admin@123";
        if (lowerName.Contains("phone")) return "0900000000";
        if (lowerName.Contains("code")) return "AUTO_${unique}";
        return "${unique}";
    }

    private static Dictionary<string, object> BuildResponseAssertions(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types)
    {
        Dictionary<string, object> assertions = new() { ["$.message"] = new { exists = true, type = "string" } };
        if (endpoint.ResponseType is null) return assertions;
        assertions["$.data"] = new { exists = true };
        AddResponseAssertions(assertions, "$.data", endpoint.ResponseType, types, 0);
        return assertions;
    }

    private static void AddResponseAssertions(Dictionary<string, object> assertions, string path, string type, IReadOnlyDictionary<string, TypeContract> types, int depth)
    {
        if (depth > 2 || !TryType(types, type, out TypeContract? response)) return;
        string[] arguments = GenericArguments(type);
        foreach (PropertyContract property in AllProperties(response, types).Take(8))
        {
            string propertyPath = $"{path}.{property.JsonName}";
            assertions[propertyPath] = new { exists = true, type = JsonType(property) };
            string nestedType = SubstituteGeneric(property.IsCollection ? property.ItemType ?? string.Empty : property.Type, response.TypeParameters, arguments);
            if (!property.IsCollection) AddResponseAssertions(assertions, propertyPath, nestedType, types, depth + 1);
        }
    }

    private static string JsonType(PropertyContract property) { if (property.IsCollection) return "array"; string type = CleanType(property.Type).ToLowerInvariant(); if (type is "bool" or "boolean") return "boolean"; if (Regex.IsMatch(type, "^(byte|short|int|int32|long|int64|float|double|decimal)$")) return "number"; return "string"; }
    private static PropertyContract? FindProperty(EndpointContract endpoint, string path, IReadOnlyDictionary<string, TypeContract> types) { ParameterContract? parameter = endpoint.Parameters.FirstOrDefault(item => item.Source is "body" or "query"); if (parameter is null || !TryType(types, parameter.Type, out TypeContract? type)) return null; string first = path.Split('.')[0]; return AllProperties(type, types).FirstOrDefault(property => property.Name.Equals(first, StringComparison.OrdinalIgnoreCase) || property.JsonName.Equals(first, StringComparison.OrdinalIgnoreCase)); }
    private static BoundaryValue InvalidValue(ValidationContract rule, PropertyContract? property) { bool resolved = TryNumbers(rule.Value, out int[] values); object? value = rule.Rule switch { "NotEmpty" => property is { IsCollection: true } ? Array.Empty<object>() : string.Empty, "NotNull" => null, "EmailAddress" => "invalid-email", "MaximumLength" when resolved => new string('x', values[0] + 1), "MinimumLength" when resolved => new string('x', Math.Max(0, values[0] - 1)), "Length" when resolved => new string('x', values.Length > 1 ? values[1] + 1 : Math.Max(0, values[0] - 1)), "GreaterThan" when resolved => values[0], "GreaterThanOrEqualTo" when resolved => values[0] - 1, "LessThan" when resolved => values[0], "LessThanOrEqualTo" when resolved => values[0] + 1, "InclusiveBetween" or "ExclusiveBetween" when resolved && values.Length > 1 => values[1] + 1, "IsInEnum" => 999999, _ => "__unresolved_boundary__" }; return new(value, rule.Rule is "NotEmpty" or "NotNull" or "EmailAddress" or "IsInEnum" || resolved); }
    private static void SetNested(Dictionary<string, object?> body, string propertyPath, object? value) { string[] parts = propertyPath.Split('.'); Dictionary<string, object?> current = body; for (int i = 0; i < parts.Length - 1; i++) { string key = current.Keys.FirstOrDefault(item => item.Equals(parts[i], StringComparison.OrdinalIgnoreCase)) ?? Camel(parts[i]); if (current.GetValueOrDefault(key) is not Dictionary<string, object?> nested) current[key] = nested = []; current = nested; } string leaf = current.Keys.FirstOrDefault(item => item.Equals(parts[^1], StringComparison.OrdinalIgnoreCase)) ?? Camel(parts[^1]); current[leaf] = value; }
    private static string SetQueryValue(string path, string property, object? value) { string key = property.Split('.')[0]; string encoded = Uri.EscapeDataString(value?.ToString() ?? string.Empty); return Regex.IsMatch(path, $"([?&]){Regex.Escape(key)}=[^&]*", RegexOptions.IgnoreCase) ? Regex.Replace(path, $"([?&]){Regex.Escape(key)}=[^&]*", $"$1{key}={encoded}", RegexOptions.IgnoreCase) : path + (path.Contains('?') ? "&" : "?") + key + "=" + encoded; }
    private static int[] SuccessStatuses(EndpointContract endpoint) { int[] result = endpoint.ResponseStatuses.Where(status => status is >= 200 and < 300).ToArray(); return result.Length > 0 ? result : [200, 201, 202, 204]; }
    private static int[] ErrorStatuses(EndpointContract endpoint) { int[] result = endpoint.ResponseStatuses.Where(status => status is >= 400 and < 500).ToArray(); return result.Length > 0 ? result : [400, 422]; }
    private static bool IsPaging(string name) => name.Equals("page", StringComparison.OrdinalIgnoreCase) || name.Equals("pageSize", StringComparison.OrdinalIgnoreCase) || name.Equals("current", StringComparison.OrdinalIgnoreCase) || name.Equals("currentPage", StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<PropertyContract> AllProperties(TypeContract type, IReadOnlyDictionary<string, TypeContract> types) => AllProperties(type, types, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    private static IEnumerable<PropertyContract> AllProperties(TypeContract type, IReadOnlyDictionary<string, TypeContract> types, HashSet<string> visited) { if (!visited.Add(type.Name)) yield break; foreach (string baseType in type.BaseTypes) if (!baseType.Equals(type.Name, StringComparison.OrdinalIgnoreCase) && types.TryGetValue(baseType, out TypeContract? parent)) foreach (PropertyContract property in AllProperties(parent, types, visited)) yield return property; foreach (PropertyContract property in type.Properties) yield return property; }
    private static bool TryNumbers(string? value, out int[] values) { if (string.IsNullOrWhiteSpace(value)) { values = []; return false; } string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); values = new int[parts.Length]; for (int index = 0; index < parts.Length; index++) if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index])) { values = []; return false; } return values.Length > 0; }
    private static string[] GenericArguments(string type) { int start = type.IndexOf('<'); if (start < 0 || !type.EndsWith('>')) return []; string inner = type[(start + 1)..^1]; List<string> result = []; int depth = 0; int segmentStart = 0; for (int index = 0; index < inner.Length; index++) { if (inner[index] == '<') depth++; else if (inner[index] == '>') depth--; else if (inner[index] == ',' && depth == 0) { result.Add(inner[segmentStart..index].Trim()); segmentStart = index + 1; } } result.Add(inner[segmentStart..].Trim()); return result.ToArray(); }
    private static string SubstituteGeneric(string propertyType, IReadOnlyList<string> parameters, string[] arguments) { for (int index = 0; index < Math.Min(parameters.Count, arguments.Length); index++) propertyType = Regex.Replace(propertyType, $@"\b{Regex.Escape(parameters[index])}\b", arguments[index]); return propertyType; }
    private static bool TryType(IReadOnlyDictionary<string, TypeContract> types, string type, out TypeContract contract) => types.TryGetValue(TypeKey(type), out contract!);
    private static string TypeKey(string type) { string clean = type.Trim().TrimEnd('?'); int start = clean.IndexOf('<'); if (start < 0) return CleanType(clean); return $"{CleanType(clean)}`{GenericArguments(clean).Length}"; }
    private static string CleanType(string type) { string clean = type.Trim().TrimEnd('?'); int generic = clean.IndexOf('<'); if (generic >= 0) clean = clean[..generic]; return clean.Split('.').Last(); }
    private static string Group(EndpointContract endpoint) => endpoint.Controller.Replace("Controller", string.Empty).ToLowerInvariant();
    private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    private static string Camel(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    private static object Coverage(string status, string[]? evidenceSteps, string? reason) => new { status, evidenceSteps, reason };
    private sealed record BoundaryValue(object? Value, bool Resolved);
}
