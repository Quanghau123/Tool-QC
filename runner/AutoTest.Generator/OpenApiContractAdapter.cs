using System.Text.Json;

namespace AutoTest.Generator;

public static class OpenApiContractAdapter
{
    public static GeneratorAnalysis Analyze(string document, string sourceName)
    {
        using JsonDocument parsed = JsonDocument.Parse(document);
        JsonElement root = parsed.RootElement;
        if (!root.TryGetProperty("openapi", out JsonElement version) || !version.GetString()!.StartsWith("3.1", StringComparison.Ordinal))
            throw new InvalidDataException("Only OpenAPI 3.1 documents are supported.");

        JsonElement schemas = root.TryGetProperty("components", out JsonElement components)
            && components.TryGetProperty("schemas", out JsonElement componentSchemas)
            ? componentSchemas : default;
        Dictionary<string, TypeContract> types = ReadTypes(schemas, sourceName);
        List<EndpointContract> endpoints = ReadEndpoints(root, schemas, sourceName);
        AuthenticationDiscovery authentication = new([], [], null);

        return new GeneratorAnalysis(
            sourceName,
            1,
            endpoints.OrderBy(endpoint => endpoint.Path).ThenBy(endpoint => endpoint.Method).ToArray(),
            types,
            [],
            authentication)
        {
            AnalysisProvider = "openapi-3.1",
            AnalysisMessage = "Source analyzed from OpenAPI 3.1."
        };
    }

    private static Dictionary<string, TypeContract> ReadTypes(JsonElement schemas, string sourceName)
    {
        Dictionary<string, TypeContract> types = new(StringComparer.OrdinalIgnoreCase);
        if (schemas.ValueKind != JsonValueKind.Object)
            return types;

        foreach (JsonProperty schema in schemas.EnumerateObject())
        {
            JsonElement required = schema.Value.TryGetProperty("required", out JsonElement requiredProperties)
                ? requiredProperties : default;
            List<PropertyContract> properties = [];
            if (schema.Value.TryGetProperty("properties", out JsonElement values) && values.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in values.EnumerateObject())
                    properties.Add(ReadProperty(property.Name, property.Value, required, schemas));

            string kind = schema.Value.TryGetProperty("type", out JsonElement type) && type.GetString() == "array" ? "array" : "class";
            types[schema.Name] = new(schema.Name, kind, 0, [], [], properties, [], sourceName);
        }

        return types;
    }

    private static List<EndpointContract> ReadEndpoints(JsonElement root, JsonElement schemas, string sourceName)
    {
        List<EndpointContract> endpoints = [];
        if (!root.TryGetProperty("paths", out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
            return endpoints;

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (!IsMethod(operation.Name))
                    continue;

                List<ParameterContract> parameters = ReadParameters(operation.Value, schemas);
                if (operation.Value.TryGetProperty("requestBody", out JsonElement requestBody))
                    parameters.Add(new("body", ReadRequestType(requestBody, schemas), "body", IsRequired(requestBody), false));

                (int[] statuses, string? responseType) = ReadResponses(operation.Value, schemas);
                bool requiresAuthentication = operation.Value.TryGetProperty("security", out JsonElement security) && security.ValueKind == JsonValueKind.Array && security.GetArrayLength() > 0;
                string action = operation.Value.TryGetProperty("operationId", out JsonElement operationId) && !string.IsNullOrWhiteSpace(operationId.GetString())
                    ? operationId.GetString()! : $"{operation.Name}-{path.Name}";
                endpoints.Add(new(
                    Slug($"{operation.Name}-{path.Name}"),
                    "OpenApi",
                    action,
                    operation.Name.ToUpperInvariant(),
                    path.Name,
                    sourceName,
                    0,
                    requiresAuthentication,
                    !requiresAuthentication,
                    "business",
                    [],
                    statuses,
                    responseType,
                    parameters,
                    [],
                    [],
                    []));
            }
        }

        return endpoints;
    }

    private static List<ParameterContract> ReadParameters(JsonElement operation, JsonElement schemas)
    {
        List<ParameterContract> parameters = [];
        if (!operation.TryGetProperty("parameters", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return parameters;

        foreach (JsonElement parameter in values.EnumerateArray())
        {
            if (!parameter.TryGetProperty("name", out JsonElement name) || !parameter.TryGetProperty("in", out JsonElement source))
                continue;
            JsonElement schema = parameter.TryGetProperty("schema", out JsonElement value) ? value : default;
            parameters.Add(new(name.GetString()!, SchemaType(schema, schemas), source.GetString()!, IsRequired(parameter), IsNullable(schema)));
        }

        return parameters;
    }

    private static (int[] Statuses, string? ResponseType) ReadResponses(JsonElement operation, JsonElement schemas)
    {
        if (!operation.TryGetProperty("responses", out JsonElement responses) || responses.ValueKind != JsonValueKind.Object)
            return ([], null);

        List<int> statuses = [];
        string? responseType = null;
        foreach (JsonProperty response in responses.EnumerateObject())
        {
            if (!int.TryParse(response.Name, out int status))
                continue;
            statuses.Add(status);
            if (responseType is null && status is >= 200 and < 300)
                responseType = ReadContentType(response.Value, schemas);
        }

        return (statuses.Distinct().Order().ToArray(), responseType);
    }

    private static string ReadRequestType(JsonElement requestBody, JsonElement schemas) => ReadContentType(requestBody, schemas) ?? "object";

    private static string? ReadContentType(JsonElement value, JsonElement schemas)
    {
        if (!value.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Object)
            return null;
        JsonProperty json = content.EnumerateObject().FirstOrDefault(item => item.Name.Equals("application/json", StringComparison.OrdinalIgnoreCase));
        return json.Value.ValueKind == JsonValueKind.Undefined || !json.Value.TryGetProperty("schema", out JsonElement schema)
            ? null : SchemaType(schema, schemas);
    }

    private static PropertyContract ReadProperty(string name, JsonElement schema, JsonElement required, JsonElement schemas)
    {
        bool collection = schema.TryGetProperty("type", out JsonElement type) && type.GetString() == "array";
        string? itemType = collection && schema.TryGetProperty("items", out JsonElement items) ? SchemaType(items, schemas) : null;
        return new(name, name, SchemaType(schema, schemas), IsRequired(required, name), IsNullable(schema), collection, itemType);
    }

    private static string SchemaType(JsonElement schema, JsonElement schemas)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return "object";
        if (schema.TryGetProperty("$ref", out JsonElement reference))
            return reference.GetString()!.Split('/').Last();
        if (schema.TryGetProperty("type", out JsonElement type))
            return type.GetString() switch
            {
                "integer" => "int",
                "number" => "decimal",
                "boolean" => "bool",
                "string" => "string",
                "array" => schema.TryGetProperty("items", out JsonElement items) ? $"List<{SchemaType(items, schemas)}>" : "List<object>",
                _ => "object"
            };
        return "object";
    }

    private static bool IsMethod(string value) => value.Equals("get", StringComparison.OrdinalIgnoreCase)
        || value.Equals("post", StringComparison.OrdinalIgnoreCase)
        || value.Equals("put", StringComparison.OrdinalIgnoreCase)
        || value.Equals("patch", StringComparison.OrdinalIgnoreCase)
        || value.Equals("delete", StringComparison.OrdinalIgnoreCase);

    private static bool IsRequired(JsonElement value) => value.TryGetProperty("required", out JsonElement required) && required.ValueKind == JsonValueKind.True;

    private static bool IsRequired(JsonElement values, string name) => values.ValueKind == JsonValueKind.Array
        && values.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String && value.GetString()!.Equals(name, StringComparison.Ordinal));

    private static bool IsNullable(JsonElement schema) => schema.TryGetProperty("type", out JsonElement type)
        && type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(value => value.GetString() == "null");

    private static string Slug(string value) => string.Concat(value.Select(character => char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')).Trim('-');
}
