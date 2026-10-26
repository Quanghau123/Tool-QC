using System.Security.Cryptography;
using System.Text;

namespace AutoTest.Generator;

public static class ApiBuilderContractProjection
{
    private const int MaximumTreeDepth = 8;

    public static ApiBuilderContract Create(GeneratorAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        BuilderOperation[] operations = analysis.Endpoints
            .Select(endpoint => CreateOperation(endpoint, analysis.Types))
            .OrderBy(operation => operation.Path, StringComparer.Ordinal)
            .ThenBy(operation => operation.Method, StringComparer.Ordinal)
            .ToArray();
        return new(
            Fingerprint(analysis),
            analysis.AnalysisProvider,
            analysis.SourceDirectory,
            operations,
            analysis.Warnings);
    }

    private static BuilderOperation CreateOperation(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types)
    {
        BuilderField[] requestFields = endpoint.Parameters
            .SelectMany(parameter => ExpandParameter(parameter, types))
            .GroupBy(field => field.JsonPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(field => field.Location, StringComparer.Ordinal)
            .ThenBy(field => field.JsonPath, StringComparer.Ordinal)
            .ToArray();
        BuilderField[] responseFields = endpoint.ResponseType is not null && ResolveType(endpoint.ResponseType, types) is { } response
            ? ExpandType(response.Type, "response", "$.data", types, response.TypeArguments, 0)
                .GroupBy(field => field.JsonPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray()
            : [];
        return new(endpoint.Action, endpoint.Action, endpoint.Controller.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ? endpoint.Controller[..^"Controller".Length] : endpoint.Controller, endpoint.Method, endpoint.Path, endpoint.SourceFile, endpoint.SourceLine, requestFields, responseFields);
    }

    private static IEnumerable<BuilderField> ExpandParameter(ParameterContract parameter, IReadOnlyDictionary<string, TypeContract> types)
    {
        string location = parameter.Source.ToLowerInvariant();
        if (location is ("body" or "query") && ResolveType(parameter.Type, types) is { } type)
            return ExpandType(type.Type, location, location == "body" ? "$" : "$.query", types, type.TypeArguments, 0);
        return [new(parameter.Name, parameter.Name, location, $"$.{location}.{parameter.Name}", parameter.Type, parameter.Required, parameter.Nullable, parameter.Type.StartsWith("List<", StringComparison.Ordinal), parameter.Type, false)];
    }

    private static IEnumerable<BuilderField> ExpandType(TypeContract type, string location, string prefix, IReadOnlyDictionary<string, TypeContract> types, IReadOnlyDictionary<string, string> typeArguments, int depth)
    {
        if (depth >= MaximumTreeDepth)
            yield break;
        foreach (TypeContract baseType in type.BaseTypes.Select(baseName => ResolveType(baseName, types)).Where(resolved => resolved is not null).Select(resolved => resolved!.Value.Type))
            foreach (BuilderField field in ExpandType(baseType, location, prefix, types, typeArguments, depth + 1))
                yield return field;
        foreach (PropertyContract property in type.Properties)
        {
            string propertyType = SubstituteTypeArguments(property.Type, typeArguments);
            string? itemType = property.ItemType is null ? null : SubstituteTypeArguments(property.ItemType, typeArguments);
            string jsonPath = $"{prefix}.{property.JsonName}";
            yield return new(
                $"{location}:{jsonPath}",
                property.Name,
                location,
                jsonPath,
                propertyType,
                property.Required,
                property.Nullable,
                property.IsCollection,
                itemType,
                false);
            if (!property.IsCollection && ResolveType(propertyType, types) is { } nested)
                foreach (BuilderField field in ExpandType(nested.Type, location, jsonPath, types, nested.TypeArguments, depth + 1))
                    yield return field;
        }
    }

    private static ResolvedType? ResolveType(string typeName, IReadOnlyDictionary<string, TypeContract> types)
    {
        string key = TypeKey(typeName);
        if (!types.TryGetValue(key, out TypeContract? type))
            return null;
        string[] arguments = GenericArguments(typeName);
        return new(type, type.TypeParameters.Zip(arguments, (parameter, argument) => new { parameter, argument }).ToDictionary(item => item.parameter, item => item.argument, StringComparer.Ordinal));
    }

    private static string SubstituteTypeArguments(string type, IReadOnlyDictionary<string, string> typeArguments)
        => typeArguments.TryGetValue(type.TrimEnd('?'), out string? resolved) ? resolved + (type.EndsWith('?') ? "?" : string.Empty) : type;

    private readonly record struct ResolvedType(TypeContract Type, IReadOnlyDictionary<string, string> TypeArguments);

    private static string Fingerprint(GeneratorAnalysis analysis)
    {
        string value = string.Join("\n", analysis.Endpoints.Select(endpoint => $"{endpoint.Id}|{endpoint.Method}|{endpoint.Path}|{endpoint.ResponseType}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string TypeKey(string value)
    {
        string clean = value.Trim().TrimEnd('?');
        int genericStart = clean.IndexOf('<');
        return genericStart < 0
            ? clean.Split('.').Last()
            : $"{clean[..genericStart].Split('.').Last()}`{GenericArguments(clean).Length}";
    }

    private static string[] GenericArguments(string type)
    {
        int start = type.IndexOf('<');
        if (start < 0 || !type.EndsWith('>'))
            return [];
        string inner = type[(start + 1)..^1];
        List<string> result = [];
        int depth = 0;
        int segmentStart = 0;
        for (int index = 0; index < inner.Length; index++)
        {
            if (inner[index] == '<') depth++;
            else if (inner[index] == '>') depth--;
            else if (inner[index] == ',' && depth == 0)
            {
                result.Add(inner[segmentStart..index].Trim());
                segmentStart = index + 1;
            }
        }
        result.Add(inner[segmentStart..].Trim());
        return result.ToArray();
    }
}

public sealed record ApiBuilderContract(
    string Fingerprint,
    string Provider,
    string Source,
    IReadOnlyList<BuilderOperation> Operations,
    IReadOnlyList<string> Warnings);

public sealed record BuilderOperation(
    string Id,
    string Name,
    string Group,
    string Method,
    string Path,
    string SourceFile,
    int SourceLine,
    IReadOnlyList<BuilderField> RequestFields,
    IReadOnlyList<BuilderField> ResponseFields);

public sealed record BuilderField(
    string Id,
    string Name,
    string Location,
    string JsonPath,
    string Type,
    bool Required,
    bool Nullable,
    bool IsCollection,
    string? ItemType,
    bool Sensitive);
