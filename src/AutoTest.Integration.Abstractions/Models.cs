using System.Text.Json;

namespace AutoTest.Integration.Abstractions;

public sealed record IntegrationProfile(
    string Name,
    string Transport,
    string Url,
    List<HttpIntegrationRule> Routes,
    bool PersistArtifacts = true,
    long MaxRequestBodyBytes = 10 * 1024 * 1024,
    int MaxInMemoryExchanges = 1000);

public sealed record HttpIntegrationRule(
    string Method,
    string Path,
    int Status,
    JsonElement? Response,
    Dictionary<string, string>? Headers,
    int DelayMs = 0,
    string? Name = null,
    Dictionary<string, JsonElement>? MatchJson = null,
    List<HttpIntegrationResponse>? Sequence = null);

public sealed record HttpIntegrationResponse(int Status, JsonElement? Response, Dictionary<string, string>? Headers = null, int DelayMs = 0);

public sealed record CapturedExchange(
    int Number,
    Guid CorrelationId,
    string Method,
    string Path,
    Dictionary<string, string> Headers,
    object? RequestBody,
    DateTimeOffset ReceivedAt,
    string? MatchedRule,
    int ResponseStatus,
    object? ResponseBody,
    long RequestBytes,
    long ResponseBytes,
    long DurationMs,
    string? Error = null);

public sealed record IntegrationSession(
    string SessionId,
    string Project,
    string Integration,
    string Transport,
    string Url,
    string ConfigPath,
    int ProcessId,
    DateTimeOffset StartedAt,
    string Status,
    string InstanceToken,
    DateTimeOffset? StoppedAt = null,
    int RequestCount = 0,
    string? ArtifactError = null);
