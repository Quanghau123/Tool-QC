using System.Text.Json;
using System.Text.Json.Serialization;
namespace AutoTest.Abstractions;
public sealed record ProjectSpec(string Name,string BaseUrlVariable,Dictionary<string,string>? DefaultHeaders,AuthSpec? Authentication,SafetySpec? Safety);
public sealed record AuthSpec(string Strategy,string? LoginPath,string? Method,JsonElement? Body,string? TokenPath,string? Header,string? Prefix);
public sealed record SafetySpec(string[]? ProductionHosts);
public sealed record SuiteSpec(string Project,List<CaseSpec> Cases);
public sealed record CaseSpec(string Id,string Name,string[]? Tags,bool Destructive,Dictionary<string,string>? Variables,List<StepSpec> Steps,List<StepSpec>? Cleanup)
{
    [JsonIgnore]
    public string SourceGroup { get; init; } = "_root";
}
public sealed record StepSpec(string Name,string? Auth,string? AuthToken,RequestSpec? Request,ExpectSpec? Expect,Dictionary<string,string>? Save,RetrySpec? Retry,int? ParallelRequests,List<ConcurrentRequestSpec>? ConcurrentRequests);
public sealed record RetrySpec(int? TimeoutMs,int? IntervalMs);
public sealed record RequestSpec(string? Method,string? Path,JsonElement? Body,Dictionary<string,string>? Form,Dictionary<string,string>? Headers,MqttRequestSpec? Mqtt,DatabaseRequestSpec? Database,HttpStubRequestSpec? HttpStub = null);
public sealed record ConcurrentRequestSpec(string Name,string? Auth,string? AuthToken,RequestSpec Request,ExpectSpec Expect);
public sealed record DatabaseRequestSpec(string Command,Dictionary<string,string>? Parameters);
public sealed record MqttRequestSpec(string Action,string? Topic,string? Payload,int? Qos,bool? Retain,int? TimeoutMs,string? Username,string? Password,string? ClientId,MqttWillSpec? Will);
public sealed record MqttWillSpec(string? Topic,string? Payload,int? Qos,bool? Retain);
public sealed record HttpStubRequestSpec(string Action,string? Method,string? Path,int? Status,JsonElement? Response,Dictionary<string,string>? ResponseHeaders,int? DelayMs,int? TimeoutMs);
public sealed record ExpectSpec(int? Status,int[]? StatusOneOf,int? MaxResponseTimeMs,Dictionary<string,AssertionSpec>? Json,MqttExpectSpec? Mqtt,DatabaseExpectSpec? Database,HttpStubExpectSpec? HttpStub = null);
public sealed record MqttExpectSpec(string? Topic,string? Payload,string? PayloadContains);
public sealed record DatabaseExpectSpec(string? ScalarEquals, bool? ResultSet);
public sealed record HttpStubExpectSpec(int? ReceivedCount,string? Method,string? Path,Dictionary<string,AssertionSpec>? Json);
public sealed class AssertionSpec
{
    [JsonPropertyName("equals")] public JsonElement? ExpectedValue { get; init; }
    public JsonElement? NotEquals { get; init; }
    public bool? Exists { get; init; }
    public string? Type { get; init; }
    public string? Contains { get; init; }
    public string? Matches { get; init; }
    public decimal? GreaterThan { get; init; }
    public decimal? GreaterThanOrEqual { get; init; }
    public decimal? LessThan { get; init; }
    public decimal? LessThanOrEqual { get; init; }
    public int? Count { get; init; }
    public JsonElement[]? OneOf { get; init; }
}
public sealed record StepRunResult(string Name,bool Cleanup,bool Passed,string Method,string Path,string? Payload,string Expected,int? ActualStatus,string? ActualResponse,TimeSpan Duration,string? Error);
public sealed record RunResult(string Id,string Name,bool Passed,TimeSpan Duration,string? Error,IReadOnlyList<StepRunResult> Steps);
