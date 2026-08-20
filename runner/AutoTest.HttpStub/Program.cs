using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

string? Argument(string key)
{
    int index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Cách dùng: dotnet run --project runner/AutoTest.HttpStub -- --config <stub.json> [--url http://127.0.0.1:2669]");
    return;
}

string configArgument = Argument("--config") ?? throw new InvalidDataException("Thiếu --config <stub.json>.");
string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string configPath = Path.IsPathRooted(configArgument)
    ? Path.GetFullPath(configArgument)
    : Path.GetFullPath(Path.Combine(repositoryRoot, configArgument));
if (!File.Exists(configPath))
    throw new FileNotFoundException($"Không tìm thấy cấu hình HTTP Stub: {configPath}", configPath);
string url = Argument("--url") ?? "http://127.0.0.1:2669";
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
StubConfiguration configuration = JsonSerializer.Deserialize<StubConfiguration>(await File.ReadAllTextAsync(configPath), options)
    ?? throw new InvalidDataException("Không đọc được cấu hình HTTP Stub.");
Validate(configuration);

var captured = new ConcurrentQueue<CapturedRequest>();
var routes = new ConcurrentDictionary<string, StubRoute>(StringComparer.OrdinalIgnoreCase);
foreach (StubRoute route in configuration.Routes) routes[RouteKey(route.Method, route.Path)] = route;
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(url);
var app = builder.Build();

app.Run(async context =>
{
    if (context.Request.Path.Equals("/__autotest/health") && HttpMethods.IsGet(context.Request.Method))
    {
        await context.Response.WriteAsJsonAsync(new { status = "ready", routes = routes.Count }, context.RequestAborted);
        return;
    }
    if (context.Request.Path.Equals("/__autotest/configure") && HttpMethods.IsPut(context.Request.Method))
    {
        StubRoute? configuredRoute = await context.Request.ReadFromJsonAsync<StubRoute>(options, context.RequestAborted);
        if (configuredRoute is null) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "HTTP_STUB_INVALID_RULE" }, context.RequestAborted); return; }
        ValidateRoute(configuredRoute);
        routes[RouteKey(configuredRoute.Method, configuredRoute.Path)] = configuredRoute;
        await context.Response.WriteAsJsonAsync(new { configured = true, method = configuredRoute.Method.ToUpperInvariant(), configuredRoute.Path, configuredRoute.Status, configuredRoute.DelayMs }, context.RequestAborted);
        return;
    }
    if (context.Request.Path.Equals("/__autotest/requests") && HttpMethods.IsGet(context.Request.Method))
    {
        await context.Response.WriteAsJsonAsync(new { count = captured.Count, requests = captured.ToArray() }, context.RequestAborted);
        return;
    }
    if (context.Request.Path.Equals("/__autotest/requests") && HttpMethods.IsDelete(context.Request.Method))
    {
        while (captured.TryDequeue(out _)) { }
        await context.Response.WriteAsJsonAsync(new { cleared = true }, context.RequestAborted);
        return;
    }

    string body;
    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8)) body = await reader.ReadToEndAsync(context.RequestAborted);
    object? parsedBody = ParseJson(body);
    var headers = context.Request.Headers.Where(x => !Sensitive(x.Key))
        .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    captured.Enqueue(new(context.Request.Method, context.Request.Path + context.Request.QueryString, headers, parsedBody, DateTimeOffset.UtcNow));

    routes.TryGetValue(RouteKey(context.Request.Method, context.Request.Path.Value ?? "/"), out StubRoute? route);
    if (route is null)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { message = "HTTP_STUB_RULE_NOT_FOUND" }, context.RequestAborted);
        return;
    }
    if (route.DelayMs > 0) await Task.Delay(route.DelayMs, context.RequestAborted);
    context.Response.StatusCode = route.Status;
    foreach ((string name, string value) in route.Headers ?? []) context.Response.Headers[name] = value;
    if (route.Response is { } response)
    {
        context.Response.ContentType = route.Headers?.GetValueOrDefault("Content-Type") ?? "application/json";
        string responseText = Render(response.GetRawText(), parsedBody);
        await context.Response.WriteAsync(responseText, context.RequestAborted);
    }
});

Console.WriteLine($"HTTP Stub đang chạy liên tục tại {url}");
Console.WriteLine($"Cấu hình: {configPath}");
Console.WriteLine("Dừng bằng Ctrl+C.");
await app.RunAsync();

static string Render(string template, object? requestBody)
{
    if (requestBody is not JsonElement root) return template;
    template = System.Text.RegularExpressions.Regex.Replace(template, @"\$\{requestJson:(\$\.[^}]+)\}", match =>
    {
        string path = match.Groups[1].Value;
        if (!TrySelect(root, path, out JsonElement value)) throw new InvalidDataException($"Không tìm thấy request placeholder: {path}");
        return value.GetRawText();
    });
    return System.Text.RegularExpressions.Regex.Replace(template, @"\$\{request:(\$\.[^}]+)\}", match =>
    {
        string path = match.Groups[1].Value;
        if (!TrySelect(root, path, out JsonElement value)) throw new InvalidDataException($"Không tìm thấy request placeholder: {path}");
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    });
}

static bool TrySelect(JsonElement root, string path, out JsonElement selected)
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

static object? ParseJson(string body)
{
    if (string.IsNullOrWhiteSpace(body)) return null;
    try { return JsonSerializer.Deserialize<JsonElement>(body); }
    catch (JsonException) { return body; }
}

static bool Sensitive(string name) => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
    name.Contains("token", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
    name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("key", StringComparison.OrdinalIgnoreCase);

static void Validate(StubConfiguration configuration)
{
    if (configuration.Routes.Count == 0) throw new InvalidDataException("HTTP Stub phải có ít nhất một route.");
    foreach (StubRoute route in configuration.Routes) ValidateRoute(route);
}

static void ValidateRoute(StubRoute route)
{
    if (string.IsNullOrWhiteSpace(route.Method)) throw new InvalidDataException("Route thiếu method.");
    if (string.IsNullOrWhiteSpace(route.Path) || !route.Path.StartsWith('/')) throw new InvalidDataException("Route path phải bắt đầu bằng '/'.");
    if (route.Status is < 100 or > 599) throw new InvalidDataException($"Status không hợp lệ tại {route.Path}.");
    if (route.DelayMs is < 0 or > 300000) throw new InvalidDataException($"DelayMs không hợp lệ tại {route.Path}.");
}

static string RouteKey(string method, string path) => $"{method.ToUpperInvariant()} {path}";

sealed record StubConfiguration(List<StubRoute> Routes);
sealed record StubRoute(string Method, string Path, int Status, JsonElement? Response, Dictionary<string, string>? Headers, int DelayMs = 0);
sealed record CapturedRequest(string Method, string Path, Dictionary<string, string> Headers, object? Body, DateTimeOffset ReceivedAt);
