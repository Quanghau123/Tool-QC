using System.Text.Json;
using AutoTest.Integration.Abstractions;

namespace AutoTest.Integration.Http;

public static class HttpProfileValidator
{
    public static void Validate(IntegrationProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new InvalidDataException("Integration profile thiếu name.");
        if (!profile.Transport.Equals("http", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("HTTP provider yêu cầu transport=http.");
        if (!Uri.TryCreate(profile.Url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttp) throw new InvalidDataException("Integration HTTP profile có URL không hợp lệ.");
        if (profile.Routes.Count == 0) throw new InvalidDataException("Integration HTTP profile phải có ít nhất một route.");
        if (profile.MaxRequestBodyBytes is < 1 or > 1024L * 1024 * 1024) throw new InvalidDataException("maxRequestBodyBytes phải từ 1 byte đến 1 GiB.");
        if (profile.MaxInMemoryExchanges is < 1 or > 100000) throw new InvalidDataException("maxInMemoryExchanges phải từ 1 đến 100000.");
        foreach (HttpIntegrationRule rule in profile.Routes) ValidateRule(rule);
    }
    public static void ValidateRule(HttpIntegrationRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Method)) throw new InvalidDataException("Route thiếu method.");
        if (string.IsNullOrWhiteSpace(rule.Path) || !rule.Path.StartsWith('/')) throw new InvalidDataException("Route path phải bắt đầu bằng '/'.");
        if (rule.Status is < 100 or > 599) throw new InvalidDataException($"Status không hợp lệ tại {rule.Path}.");
        if (rule.DelayMs is < 0 or > 300000) throw new InvalidDataException($"DelayMs không hợp lệ tại {rule.Path}.");
        foreach (HttpIntegrationResponse response in rule.Sequence ?? [])
        {
            if (response.Status is < 100 or > 599) throw new InvalidDataException($"Sequence status không hợp lệ tại {rule.Path}.");
            if (response.DelayMs is < 0 or > 300000) throw new InvalidDataException($"Sequence delayMs không hợp lệ tại {rule.Path}.");
        }
    }
}

public static class HttpTemplateRenderer
{
    public static string Render(string template, object? requestBody)
    {
        if (requestBody is not JsonElement root) return template;
        template = System.Text.RegularExpressions.Regex.Replace(template, @"\$\{requestJson:(\$\.[^}]+)\}", match => Select(root, match.Groups[1].Value).GetRawText());
        return System.Text.RegularExpressions.Regex.Replace(template, @"\$\{request:(\$\.[^}]+)\}", match =>
        {
            JsonElement value = Select(root, match.Groups[1].Value);
            return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        });
    }
    private static JsonElement Select(JsonElement root, string path)
    {
        JsonElement selected = root;
        if (!path.StartsWith("$.", StringComparison.Ordinal)) throw new InvalidDataException($"JSON path không hợp lệ: {path}");
        foreach (string part in path[2..].Split('.'))
        {
            if (selected.ValueKind == JsonValueKind.Object && selected.TryGetProperty(part, out JsonElement property)) selected = property;
            else if (selected.ValueKind == JsonValueKind.Array && int.TryParse(part, out int index) && index >= 0 && index < selected.GetArrayLength()) selected = selected[index];
            else throw new InvalidDataException($"Không tìm thấy request placeholder: {path}");
        }
        return selected;
    }
}

public static class JsonRuleMatcher
{
    public static bool Matches(Dictionary<string, JsonElement>? assertions, object? body)
    {
        if (assertions is not { Count: > 0 }) return true;
        if (body is not JsonElement root) return false;
        foreach ((string path, JsonElement expected) in assertions)
        {
            if (!TrySelect(root, path, out JsonElement actual) || actual.GetRawText() != expected.GetRawText()) return false;
        }
        return true;
    }
    private static bool TrySelect(JsonElement root, string path, out JsonElement selected)
    {
        selected = root;
        if (!path.StartsWith("$.", StringComparison.Ordinal)) return false;
        foreach (string part in path[2..].Split('.'))
        {
            if (selected.ValueKind == JsonValueKind.Object && selected.TryGetProperty(part, out JsonElement property)) selected = property;
            else if (selected.ValueKind == JsonValueKind.Array && int.TryParse(part, out int index) && index >= 0 && index < selected.GetArrayLength()) selected = selected[index];
            else return false;
        }
        return true;
    }
}
