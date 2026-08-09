using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AutoTest.Core;

public sealed class RunnerEngine : IDisposable
{
    private readonly ProjectSpec project;
    private readonly EnvironmentStore env;
    private readonly HttpClient http;
    private string? token;

    public RunnerEngine(ProjectSpec project, EnvironmentStore env)
    {
        this.project = project;
        this.env = env;
        var url = env.Require(project.BaseUrlVariable);
        Guard(url);
        http = new HttpClient
        {
            BaseAddress = new Uri(url),
            Timeout = TimeSpan.FromSeconds(int.TryParse(env.Get("API_TIMEOUT_SECONDS"), out var seconds) ? seconds : 30)
        };
        foreach (var header in project.DefaultHeaders ?? [])
            http.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    public async Task<RunResult> RunAsync(CaseSpec test, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var started = false;
        var stepResults = new List<StepRunResult>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unique"] = Guid.NewGuid().ToString("N"),
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };

        try
        {
            if (test.Destructive && !env.Bool("ALLOW_DESTRUCTIVE_TESTS"))
                throw new InvalidOperationException("Destructive test is blocked.");
            foreach (var variable in test.Variables ?? [])
                variables[variable.Key] = Templates.Resolve(variable.Value, variables, env);
            started = true;
            foreach (var step in test.Steps)
                await RunStepAsync(step, variables, cancellationToken, stepResults);
            return new(test.Id, test.Name, true, watch.Elapsed, null, stepResults);
        }
        catch (Exception exception)
        {
            return new(test.Id, test.Name, false, watch.Elapsed, Redact(exception.Message), stepResults);
        }
        finally
        {
            if (started)
                foreach (var step in test.Cleanup ?? [])
                    try { await RunStepAsync(step, variables, cancellationToken, stepResults, false, true); }
                    catch (Exception exception) { Console.Error.WriteLine($"Cleanup failed for {test.Id}: {Redact(exception.Message)}"); }
        }
    }

    private async Task RunStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results, bool assertions = true, bool cleanup = false)
    {
        var watch = Stopwatch.StartNew();
        var path = Templates.Resolve(step.Request.Path, variables, env);
        var payload = step.Request.Body is { } body ? Templates.Resolve(body.GetRawText(), variables, env) : null;
        var expectedText = assertions ? DescribeExpected(step.Expect, variables) : "Cleanup: không kiểm tra kết quả";
        int? actualStatus = null;
        string? responseText = null;
        try
        {
            var requiresAuth = !string.IsNullOrWhiteSpace(step.Auth);
            if (requiresAuth) await AuthenticateAsync(cancellationToken);
            using var request = new HttpRequestMessage(new HttpMethod(step.Request.Method), path);
            if (requiresAuth && token is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue(project.Authentication?.Prefix ?? "Bearer", token);
            foreach (var header in step.Request.Headers ?? [])
                request.Headers.TryAddWithoutValidation(header.Key, Templates.Resolve(header.Value, variables, env));
            if (payload is not null)
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, cancellationToken);
            actualStatus = (int)response.StatusCode;
            responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (assertions)
            {
                var expected = step.Expect ?? throw new InvalidDataException($"Missing expect: {step.Name}");
                if (actualStatus != expected.Status)
                    throw new InvalidOperationException($"{step.Name}: expected {expected.Status}, got {actualStatus}. Body: {Redact(responseText)}");
                if (expected.MaxResponseTimeMs is { } max && watch.ElapsedMilliseconds > max)
                    throw new InvalidOperationException($"Response time exceeded {max}ms.");
                AssertAndSave(step, expected, responseText, variables);
            }
            results.Add(new(step.Name, cleanup, true, step.Request.Method, path, SanitizeJson(payload), expectedText, actualStatus, SanitizeJson(responseText), watch.Elapsed, null));
        }
        catch (Exception exception)
        {
            var error = Redact(exception.Message);
            results.Add(new(step.Name, cleanup, false, step.Request.Method, path, SanitizeJson(payload), expectedText, actualStatus, SanitizeJson(responseText), watch.Elapsed, error));
            throw;
        }
    }

    private void AssertAndSave(StepSpec step, ExpectSpec expected, string responseText, Dictionary<string, string> variables)
    {
        if ((expected.Json?.Count ?? 0) == 0 && step.Save is null) return;
        using var document = JsonDocument.Parse(responseText);
        foreach (var assertion in expected.Json ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, assertion.Key);
            if (assertion.Value.Exists is { } exists && selected.Found != exists) throw new InvalidOperationException($"{assertion.Key} existence mismatch.");
            if (!selected.Found) continue;
            var actual = JsonPath.Text(selected.Value);
            if (assertion.Value.ExpectedValue is { } value && actual != Templates.Resolve(JsonPath.Text(value), variables, env)) throw new InvalidOperationException($"{assertion.Key} mismatch.");
            if (assertion.Value.Contains is { } contains && !actual.Contains(Templates.Resolve(contains, variables, env), StringComparison.Ordinal)) throw new InvalidOperationException($"{assertion.Key} contains mismatch.");
        }
        foreach (var saved in step.Save ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, saved.Value);
            if (!selected.Found) throw new InvalidOperationException($"Cannot save {saved.Key}.");
            variables[saved.Key] = JsonPath.Text(selected.Value);
        }
    }

    private string DescribeExpected(ExpectSpec? expected, Dictionary<string, string> variables)
    {
        if (expected is null) return "Thiếu cấu hình expect";
        var details = new List<string> { $"HTTP status = {expected.Status}" };
        if (expected.MaxResponseTimeMs is { } max) details.Add($"Response time <= {max} ms");
        foreach (var item in expected.Json ?? [])
        {
            var rules = new List<string>();
            if (item.Value.Exists is { } exists) rules.Add(exists ? "tồn tại" : "không tồn tại");
            if (item.Value.ExpectedValue is { } value) rules.Add($"bằng {Templates.Resolve(JsonPath.Text(value), variables, env)}");
            if (item.Value.Contains is { } contains) rules.Add($"chứa {Templates.Resolve(contains, variables, env)}");
            details.Add($"{item.Key}: {string.Join(", ", rules)}");
        }
        return string.Join("\n", details);
    }

    private string? SanitizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(SanitizeElement(document.RootElement), new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return Redact(value); }
    }

    private object? SanitizeElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => IsSensitive(property.Name) ? (object?)"***" : SanitizeElement(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(SanitizeElement).ToArray(),
        JsonValueKind.String => Redact(element.GetString() ?? string.Empty),
        JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static bool IsSensitive(string name) => new[] { "password", "confirmPassword", "token", "accessToken", "refreshToken", "authorization", "secret", "connectionString" }
        .Any(key => name.Contains(key, StringComparison.OrdinalIgnoreCase));

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (token is not null) return;
        var auth = project.Authentication ?? throw new InvalidOperationException("Authentication is not configured.");
        if (auth.Strategy.Equals("static-token", StringComparison.OrdinalIgnoreCase))
        {
            token = env.Require("AUTH_TOKEN");
            return;
        }
        if (!auth.Strategy.Equals("login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported auth: {auth.Strategy}");
        using var content = new StringContent(Templates.Resolve(auth.Body?.GetRawText() ?? "{}", new Dictionary<string, string>(), env), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(new HttpRequestMessage(new HttpMethod(auth.Method ?? "POST"), auth.LoginPath) { Content = content }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(body);
        var selected = JsonPath.Select(document.RootElement, auth.TokenPath ?? "$.accessToken");
        token = selected.Found ? JsonPath.Text(selected.Value) : throw new InvalidOperationException("Token not found.");
    }

    private void Guard(string url)
    {
        var uri = new Uri(url);
        var production = (project.Safety?.ProductionHosts ?? []).Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) ||
                         (env.Get("TEST_ENV") ?? "").Equals("production", StringComparison.OrdinalIgnoreCase);
        if (production && !env.Bool("ALLOW_PRODUCTION")) throw new InvalidOperationException("Production target is blocked.");
    }

    private string Redact(string value)
    {
        foreach (var key in new[] { "AUTH_TOKEN", "AUTH_PASSWORD", "DB_CONNECTION_STRING", "REDIS_CONNECTION_STRING" })
            if (env.Get(key) is { Length: > 0 } secret) value = value.Replace(secret, "***", StringComparison.Ordinal);
        return value;
    }

    public void Dispose() => http.Dispose();
}
