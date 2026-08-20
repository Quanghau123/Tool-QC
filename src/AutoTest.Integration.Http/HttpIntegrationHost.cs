using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoTest.Integration.Abstractions;
using AutoTest.Integration.Artifacts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Integration.Http;

public sealed class HttpIntegrationHost
{
    private readonly IntegrationProfile profile;
    private readonly IntegrationSession session;
    private readonly IntegrationArtifactWriter artifacts;
    private readonly ConcurrentDictionary<string, RuleState> rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<CapturedExchange> captured = new();
    private int sequence;

    public HttpIntegrationHost(IntegrationProfile profile, IntegrationSession session, IntegrationArtifactWriter artifacts)
    {
        this.profile = profile;
        this.session = session;
        this.artifacts = artifacts;
        HttpProfileValidator.Validate(profile);
        foreach (HttpIntegrationRule rule in profile.Routes) rules[Key(rule.Method, rule.Path)] = new(rule);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(profile.Url);
        var app = builder.Build();
        await artifacts.SessionAsync(session);

        app.MapGet("/__autotest/health", () => new
        {
            status = "ready", transport = profile.Transport, session.Project, session.Integration,
            routes = rules.Count, session.SessionId, resultRoot = artifacts.Root, session.InstanceToken,
        });
        app.MapPut("/__autotest/configure", async (HttpContext context) =>
        {
            HttpIntegrationRule? rule = await context.Request.ReadFromJsonAsync<HttpIntegrationRule>(cancellationToken: context.RequestAborted);
            if (rule is null) return Results.BadRequest(new { message = "INTEGRATION_INVALID_RULE" });
            HttpProfileValidator.ValidateRule(rule);
            rules[Key(rule.Method, rule.Path)] = new(rule);
            return Results.Ok(new { configured = true, method = rule.Method.ToUpperInvariant(), rule.Path, rule.Status, rule.DelayMs });
        });
        app.MapGet("/__autotest/requests", () => new { count = captured.Count, requests = captured.ToArray() });
        app.MapDelete("/__autotest/requests", () => { while (captured.TryDequeue(out _)) { } return Results.Ok(new { cleared = true }); });
        app.MapPost("/__autotest/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) =>
        {
            string token = context.Request.Headers["X-AutoTest-Instance"].ToString();
            if (!string.Equals(token, session.InstanceToken, StringComparison.Ordinal)) return Results.StatusCode(409);
            _ = Task.Run(async () => { await Task.Delay(100); lifetime.StopApplication(); });
            return Results.Ok(new { stopping = true, session.SessionId });
        });
        app.MapFallback(HandleAsync);

        try { await app.RunAsync(cancellationToken); }
        finally
        {
            await artifacts.SessionAsync(session with
            {
                Status = "stopped", StoppedAt = DateTimeOffset.Now, RequestCount = captured.Count,
                ArtifactError = artifacts.LastError,
            });
            await artifacts.IndexAsync(captured.ToArray());
        }
    }

    private async Task HandleAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        int number = Interlocked.Increment(ref sequence);
        Guid correlationId = Guid.NewGuid();
        DateTimeOffset receivedAt = DateTimeOffset.Now;
        object? requestBody = null;
        object? responseBody = null;
        int responseStatus = 500;
        string? matchedRule = null;
        string? error = null;
        long requestBytes = context.Request.ContentLength ?? 0;
        long responseBytes = 0;
        var headers = context.Request.Headers.ToDictionary(x => x.Key, x => HeaderRedactor.IsSensitive(x.Key) ? "***" : x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        try
        {
            if (context.Request.ContentLength is > 0 && context.Request.ContentLength > profile.MaxRequestBodyBytes)
            {
                responseStatus = 413;
                responseBody = new { message = "INTEGRATION_REQUEST_TOO_LARGE", maximumBytes = profile.MaxRequestBodyBytes };
            }
            else
            {
                string rawBody = await ReadBoundedAsync(context.Request.Body, profile.MaxRequestBodyBytes, context.RequestAborted);
                requestBytes = Encoding.UTF8.GetByteCount(rawBody);
                requestBody = JsonValue.Parse(rawBody);
                RuleState? state = Match(context.Request.Method, context.Request.Path.Value ?? "/", requestBody);
                if (state is null)
                {
                    responseStatus = 404;
                    responseBody = new { message = "INTEGRATION_RULE_NOT_FOUND" };
                }
                else
                {
                    HttpIntegrationResponse selected = state.Next();
                    matchedRule = state.Rule.Name ?? Key(state.Rule.Method, state.Rule.Path);
                    if (selected.DelayMs > 0) await Task.Delay(selected.DelayMs, context.RequestAborted);
                    responseStatus = selected.Status;
                    responseBody = selected.Response is { } template ? JsonValue.Parse(HttpTemplateRenderer.Render(template.GetRawText(), requestBody)) : null;
                    foreach ((string name, string value) in selected.Headers ?? []) context.Response.Headers[name] = value;
                }
            }
        }
        catch (PayloadTooLargeException)
        {
            responseStatus = 413;
            responseBody = new { message = "INTEGRATION_REQUEST_TOO_LARGE", maximumBytes = profile.MaxRequestBodyBytes };
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            responseStatus = 499;
            error = "Client disconnected.";
        }
        catch (Exception exception)
        {
            responseStatus = 500;
            error = exception.Message;
            responseBody = new { message = "INTEGRATION_HOST_ERROR" };
        }

        string? responseText = responseBody is null ? null : JsonSerializer.Serialize(responseBody);
        responseBytes = responseText is null ? 0 : Encoding.UTF8.GetByteCount(responseText);
        context.Response.StatusCode = responseStatus;
        if (responseText is not null && !context.RequestAborted.IsCancellationRequested)
        {
            context.Response.ContentType = context.Response.ContentType ?? "application/json";
            await context.Response.WriteAsync(responseText, context.RequestAborted);
        }
        stopwatch.Stop();
        var exchange = new CapturedExchange(number, correlationId, context.Request.Method,
            context.Request.Path + context.Request.QueryString, headers, requestBody, receivedAt,
            matchedRule, responseStatus, responseBody, requestBytes, responseBytes, stopwatch.ElapsedMilliseconds, error);
        captured.Enqueue(exchange);
        while (captured.Count > profile.MaxInMemoryExchanges) captured.TryDequeue(out _);
        if (profile.PersistArtifacts) await artifacts.ExchangeAsync(exchange);
        await artifacts.IndexAsync(captured.ToArray());
    }

    private RuleState? Match(string method, string path, object? body)
    {
        if (!rules.TryGetValue(Key(method, path), out RuleState? state)) return null;
        return JsonRuleMatcher.Matches(state.Rule.MatchJson, body) ? state : null;
    }
    private static string Key(string method, string path) => $"{method.ToUpperInvariant()} {path}";
    private static async Task<string> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > maximumBytes) throw new PayloadTooLargeException();
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
    private sealed class PayloadTooLargeException : Exception;
    private sealed class RuleState(HttpIntegrationRule rule)
    {
        private int invocation;
        public HttpIntegrationRule Rule { get; } = rule;
        public HttpIntegrationResponse Next()
        {
            int index = Interlocked.Increment(ref invocation) - 1;
            if (Rule.Sequence is { Count: > 0 }) return Rule.Sequence[Math.Min(index, Rule.Sequence.Count - 1)];
            return new(Rule.Status, Rule.Response, Rule.Headers, Rule.DelayMs);
        }
    }
}

public static class HeaderRedactor
{
    public static bool IsSensitive(string name) => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("key", StringComparison.OrdinalIgnoreCase);
}

internal static class JsonValue
{
    public static object? Parse(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { return JsonSerializer.Deserialize<JsonElement>(body); } catch (JsonException) { return body; } }
}
