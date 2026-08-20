using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using AutoTest.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.HttpStub;

internal sealed class HttpStubServer : IAsyncDisposable
{
    private readonly IEnvironmentStore environment;
    private readonly ConcurrentDictionary<string, StubRule> rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<CapturedRequest> requests = new();
    private WebApplication? app;
    private string? baseUrl;

    public HttpStubServer(IEnvironmentStore environment) => this.environment = environment;

    public async Task<object> StartAsync(CancellationToken cancellationToken)
    {
        if (app is not null) return new { baseUrl, alreadyRunning = true };
        string host = environment.Get("HTTP_STUB_HOST") ?? "127.0.0.1";
        int port = environment.Int("HTTP_STUB_PORT", 0);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{host}:{port}");
        app = builder.Build();
        app.Run(HandleAsync);
        await app.StartAsync(cancellationToken);
        baseUrl = app.Urls.Single();
        return new { baseUrl, alreadyRunning = false };
    }

    public object Configure(HttpStubRequestSpec spec, StepExecutionContext context)
    {
        EnsureRunning();
        string method = (spec.Method ?? "POST").ToUpperInvariant();
        string path = Resolve(spec.Path ?? throw new InvalidDataException("HttpStub configure thiếu path."), context);
        if (!path.StartsWith('/')) throw new InvalidDataException("HttpStub path phải bắt đầu bằng '/'.");
        int status = spec.Status ?? 200;
        if (status is < 100 or > 599) throw new InvalidDataException("HttpStub status phải từ 100 đến 599.");
        int delayMs = spec.DelayMs ?? 0;
        if (delayMs is < 0 or > 300000) throw new InvalidDataException("HttpStub delayMs phải từ 0 đến 300000.");
        string body = spec.Response is { } response ? Resolve(response.GetRawText(), context) : "";
        var headers = (spec.ResponseHeaders ?? []).ToDictionary(x => x.Key, x => Resolve(x.Value, context), StringComparer.OrdinalIgnoreCase);
        rules[Key(method, path)] = new(status, body, headers, delayMs);
        return new { method, path, status, delayMs };
    }

    public object Reset()
    {
        rules.Clear();
        while (requests.TryDequeue(out _)) { }
        return new { cleared = true };
    }

    public async Task<object> InspectAsync(HttpStubRequestSpec spec, StepExecutionContext context, CancellationToken cancellationToken)
    {
        EnsureRunning();
        int timeoutMs = spec.TimeoutMs ?? 10000;
        if (timeoutMs is < 1 or > 300000) throw new InvalidDataException("HttpStub timeoutMs phải từ 1 đến 300000.");
        string? method = spec.Method is null ? null : Resolve(spec.Method, context).ToUpperInvariant();
        string? path = spec.Path is null ? null : Resolve(spec.Path, context);
        HttpStubExpectSpec expect = context.Step.Expect?.HttpStub ?? new(null, method, path, null);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        CapturedRequest[] matches;
        do
        {
            matches = Match(method ?? expect.Method, path ?? expect.Path, expect.Json, context);
            if (expect.ReceivedCount is null || matches.Length == expect.ReceivedCount) break;
            await Task.Delay(100, cancellationToken);
        } while (DateTimeOffset.UtcNow < deadline);

        if (expect.ReceivedCount is { } count && matches.Length != count)
            throw new InvalidOperationException($"HttpStub mong đợi {count} request, thực tế {matches.Length}.");
        if (expect.Json is { Count: > 0 })
        {
            if (matches.Length == 0) throw new InvalidOperationException("HttpStub không có request để đối chiếu JSON.");
            if (matches[^1].Body is not JsonElement body)
                throw new InvalidOperationException("HttpStub request body không phải JSON hợp lệ.");
            using JsonDocument document = JsonDocument.Parse(body.GetRawText());
            foreach ((string jsonPath, AssertionSpec assertion) in expect.Json)
                AssertJson(jsonPath, assertion, document.RootElement, context);
        }
        return new { receivedCount = matches.Length, requests = matches };
    }

    public async Task<object> StopAsync(CancellationToken cancellationToken)
    {
        if (app is null) return new { stopped = false };
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
        app = null;
        baseUrl = null;
        return new { stopped = true };
    }

    private async Task HandleAsync(HttpContext context)
    {
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8)) body = await reader.ReadToEndAsync(context.RequestAborted);
        var headers = context.Request.Headers
            .Where(x => !IsSensitive(x.Key))
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        requests.Enqueue(new(context.Request.Method, context.Request.Path + context.Request.QueryString, headers, ParseJson(body), DateTimeOffset.UtcNow));
        string routePath = context.Request.Path.Value ?? "/";
        if (!rules.TryGetValue(Key(context.Request.Method, routePath), out StubRule? rule))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = "HTTP_STUB_RULE_NOT_FOUND" }, context.RequestAborted);
            return;
        }
        if (rule.DelayMs > 0) await Task.Delay(rule.DelayMs, context.RequestAborted);
        context.Response.StatusCode = rule.Status;
        foreach ((string name, string value) in rule.Headers) context.Response.Headers[name] = value;
        if (!string.IsNullOrEmpty(rule.Body))
        {
            context.Response.ContentType = rule.Headers.TryGetValue("Content-Type", out string? contentType) ? contentType : "application/json";
            await context.Response.WriteAsync(rule.Body, context.RequestAborted);
        }
    }

    private CapturedRequest[] Match(string? method, string? path, Dictionary<string, AssertionSpec>? json, StepExecutionContext context) => requests.Where(request =>
        (method is null || request.Method.Equals(method, StringComparison.OrdinalIgnoreCase)) &&
        (path is null || request.Path.Split('?')[0].Equals(path, StringComparison.OrdinalIgnoreCase)) &&
        MatchesJson(request, json, context)).ToArray();

    private static bool MatchesJson(CapturedRequest request, Dictionary<string, AssertionSpec>? assertions, StepExecutionContext context)
    {
        if (assertions is not { Count: > 0 }) return true;
        if (request.Body is not JsonElement body) return false;
        try
        {
            foreach ((string path, AssertionSpec assertion) in assertions) AssertJson(path, assertion, body, context);
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private static void AssertJson(string path, AssertionSpec assertion, JsonElement root, StepExecutionContext context)
    {
        (bool Found, JsonElement Value) selected = JsonPath.Select(root, path);
        if (assertion.Exists is { } exists && selected.Found != exists)
            throw new InvalidOperationException($"HttpStub trường {path} không đúng yêu cầu tồn tại.");
        if (assertion.ExpectedValue is { } expected)
        {
            if (!selected.Found) throw new InvalidOperationException($"HttpStub không tìm thấy trường {path}.");
            string wantedText = Templates.Resolve(expected.GetRawText(), context.Variables, context.Environment);
            using JsonDocument wanted = JsonDocument.Parse(wantedText);
            if (!JsonEquals(selected.Value, wanted.RootElement))
                throw new InvalidOperationException($"HttpStub trường {path} không đúng giá trị mong đợi.");
        }
    }

    private static bool JsonEquals(JsonElement actual, JsonElement expected)
        => actual.ValueKind == expected.ValueKind && actual.GetRawText() == expected.GetRawText();

    private static object? ParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(body); }
        catch (JsonException) { return body; }
    }

    private static bool IsSensitive(string name) => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("key", StringComparison.OrdinalIgnoreCase);
    private static string Resolve(string value, StepExecutionContext context) => Templates.Resolve(value, context.Variables, context.Environment);
    private static string Key(string method, string path) => $"{method.ToUpperInvariant()} {path}";
    private void EnsureRunning() { if (app is null) throw new InvalidOperationException("HttpStub chưa được start."); }
    public async ValueTask DisposeAsync() { if (app is not null) await StopAsync(CancellationToken.None); }

    private sealed record StubRule(int Status, string Body, Dictionary<string, string> Headers, int DelayMs);
    private sealed record CapturedRequest(string Method, string Path, Dictionary<string, string> Headers, object? Body, DateTimeOffset ReceivedAt);
}
