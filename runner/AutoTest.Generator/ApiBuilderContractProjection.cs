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
            .OrderBy(field => field.Location, StringComparer.Ordinal)
            .ThenBy(field => field.JsonPath, StringComparer.Ordinal)
            .ToArray();
        BuilderField[] responseFields = endpoint.ResponseType is not null && types.TryGetValue(TypeKey(endpoint.ResponseType), out TypeContract? response)
            ? ExpandType(response, "response", "$.data", types, 0).ToArray()
            : [];
        return new(endpoint.Action, endpoint.Action, endpoint.Method, endpoint.Path, endpoint.SourceFile, endpoint.SourceLine, requestFields, responseFields);
    }

    private static IEnumerable<BuilderField> ExpandParameter(ParameterContract parameter, IReadOnlyDictionary<string, TypeContract> types)
    {
        string location = parameter.Source.ToLowerInvariant();
        if (location != "body")
            return [new(parameter.Name, parameter.Name, location, $"$.{location}.{parameter.Name}", parameter.Type, parameter.Required, parameter.Nullable, parameter.Type.StartsWith("List<", StringComparison.Ordinal), parameter.Type, false)];
        return types.TryGetValue(TypeKey(parameter.Type), out TypeContract? type)
            ? ExpandType(type, "body", "$", types, 0)
            : [new(parameter.Name, parameter.Name, "body", "$.body", parameter.Type, parameter.Required, parameter.Nullable, false, parameter.Type, false)];
    }

    private static IEnumerable<BuilderField> ExpandType(TypeContract type, string location, string prefix, IReadOnlyDictionary<string, TypeContract> types, int depth)
    {
        if (depth >= MaximumTreeDepth)
            yield break;
        foreach (PropertyContract property in type.Properties)
        {
            string jsonPath = $"{prefix}.{property.JsonName}";
            yield return new(
                $"{location}:{jsonPath}",
                property.Name,
                location,
                jsonPath,
                property.Type,
                property.Required,
                property.Nullable,
                property.IsCollection,
                property.ItemType,
                false);
            if (!property.IsCollection && types.TryGetValue(TypeKey(property.Type), out TypeContract? nested))
                foreach (BuilderField field in ExpandType(nested, location, jsonPath, types, depth + 1))
                    yield return field;
        }
    }

    private static string Fingerprint(GeneratorAnalysis analysis)
    {
        string value = string.Join("\n", analysis.Endpoints.Select(endpoint => $"{endpoint.Id}|{endpoint.Method}|{endpoint.Path}|{endpoint.ResponseType}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string TypeKey(string value) => value.Replace("?", string.Empty, StringComparison.Ordinal).Split('<')[0].Trim();
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
