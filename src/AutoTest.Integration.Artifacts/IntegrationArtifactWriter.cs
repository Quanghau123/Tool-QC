using System.Text.Encodings.Web;
using System.Text.Json;
using AutoTest.Integration.Abstractions;

namespace AutoTest.Integration.Artifacts;

public sealed class IntegrationArtifactWriter
{
    private readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly SemaphoreSlim gate = new(1, 1);
    public string Root { get; }
    public string? LastError { get; private set; }

    public IntegrationArtifactWriter(string root)
    {
        Root = root;
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        Directory.CreateDirectory(Path.Combine(root, "responses"));
    }

    public async Task<bool> SessionAsync(IntegrationSession session)
        => await TryWriteAsync(Path.Combine(Root, "session.json"), session);

    public async Task<bool> ExchangeAsync(CapturedExchange exchange)
    {
        await gate.WaitAsync();
        try
        {
            string file = $"{exchange.Number:D6}.json";
            bool request = await TryWriteUnsafeAsync(Path.Combine(Root, "requests", file), new
            {
                transport = "http", exchange.Number, exchange.CorrelationId, exchange.ReceivedAt,
                exchange.Method, exchange.Path, exchange.Headers, payload = exchange.RequestBody,
                exchange.RequestBytes, exchange.MatchedRule, exchange.Error,
            });
            bool response = await TryWriteUnsafeAsync(Path.Combine(Root, "responses", file), new
            {
                transport = "http", exchange.Number, exchange.CorrelationId, sentAt = exchange.ReceivedAt.AddMilliseconds(exchange.DurationMs),
                status = exchange.ResponseStatus, payload = exchange.ResponseBody, exchange.ResponseBytes,
                exchange.DurationMs, exchange.MatchedRule, exchange.Error,
            });
            return request && response;
        }
        finally { gate.Release(); }
    }

    public async Task IndexAsync(IReadOnlyCollection<CapturedExchange> exchanges)
    {
        string rows = string.Join(Environment.NewLine, exchanges.Select(x => $"<tr><td>{x.Number}</td><td>{x.ReceivedAt:O}</td><td>{Html(x.Method)}</td><td>{Html(x.Path)}</td><td>{x.ResponseStatus}</td><td>{x.DurationMs}</td><td><a href='requests/{x.Number:D6}.json'>request</a></td><td><a href='responses/{x.Number:D6}.json'>response</a></td></tr>"));
        string html = $"<!doctype html><meta charset='utf-8'><title>Integration evidence</title><style>body{{font-family:Segoe UI;padding:24px}}table{{border-collapse:collapse;width:100%}}td,th{{border:1px solid #ddd;padding:8px}}</style><h1>Integration evidence</h1><p>Requests: {exchanges.Count}</p><table><thead><tr><th>#</th><th>Received</th><th>Method</th><th>Path</th><th>Status</th><th>ms</th><th>Request</th><th>Response</th></tr></thead><tbody>{rows}</tbody></table>";
        try { await File.WriteAllTextAsync(Path.Combine(Root, "index.html"), html); }
        catch (Exception exception) { LastError = exception.Message; }
    }

    private async Task<bool> TryWriteAsync(string path, object value)
    {
        await gate.WaitAsync();
        try { return await TryWriteUnsafeAsync(path, value); }
        finally { gate.Release(); }
    }
    private async Task<bool> TryWriteUnsafeAsync(string path, object value)
    {
        try { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, options)); return true; }
        catch (Exception exception) { LastError = exception.Message; return false; }
    }
    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);
}
