using System.Text.Json;
using System.Text.Json.Serialization;
namespace AutoTest.Abstractions;
public sealed record ProjectSpec(string Name,string BaseUrlVariable,Dictionary<string,string>? DefaultHeaders,AuthSpec? Authentication,SafetySpec? Safety);
public sealed record AuthSpec(string Strategy,string? LoginPath,string? Method,JsonElement? Body,string? TokenPath,string? Header,string? Prefix);
public sealed record SafetySpec(string[]? ProductionHosts);
public sealed record SuiteSpec(string Project,List<CaseSpec> Cases,int? SchemaVersion=null);
public sealed record CaseSpec(string Id,string Name,string[]? Tags,bool Destructive,Dictionary<string,string>? Variables,List<StepSpec> Steps,List<StepSpec>? Cleanup,CoverageSpec? Coverage=null)
{
    [JsonIgnore]
    public string SourceGroup { get; init; } = "_root";
    [JsonIgnore]
    public int SchemaVersion { get; init; } = 2;
}
public sealed record CoverageSpec(
    SourceVerificationSpec? SourceVerification,
    CoverageItemSpec? Contract,
    CoverageItemSpec? Validation,
    CoverageItemSpec? Authentication,
    CoverageItemSpec? Scope,
    CoverageItemSpec? Integrity,
    CoverageItemSpec? Lifecycle,
    CoverageItemSpec? Concurrency,
    CoverageItemSpec? Reporting);
public sealed record SourceVerificationSpec(string[]? Files,string? VerifiedAt,string? ContractVersion);
public sealed record CoverageItemSpec(string Status,string[]? EvidenceSteps,string? Reason);
public sealed record StepSpec(string Name,string? Auth,string? AuthToken,RequestSpec? Request,ExpectSpec? Expect,Dictionary<string,string>? Save,RetrySpec? Retry,int? ParallelRequests,List<ConcurrentRequestSpec>? ConcurrentRequests);
public sealed record RetrySpec(int? TimeoutMs,int? IntervalMs);
public sealed record RequestSpec(string? Method,string? Path,JsonElement? Body,Dictionary<string,string>? Form,Dictionary<string,string>? Headers,MqttRequestSpec? Mqtt,DatabaseRequestSpec? Database,HttpStubRequestSpec? HttpStub = null,string? RawBody = null,ElasticsearchRequestSpec? Elasticsearch = null,RedisRequestSpec? Redis = null,MultipartRequestSpec? Multipart = null);
public sealed record MultipartRequestSpec(Dictionary<string,string>? Fields,List<MultipartFileSpec>? Files);
public sealed record MultipartFileSpec(string Field,string Path,string? FileName=null,string? ContentType=null);
public sealed record ConcurrentRequestSpec(string Name,string? Auth,string? AuthToken,RequestSpec Request,ExpectSpec Expect);
public sealed record DatabaseRequestSpec(string Command,Dictionary<string,string>? Parameters,string? ConnectionStringVariable=null);
public sealed record ElasticsearchRequestSpec(string Method,string Path,JsonElement? Body=null,string? UrlVariable=null,string? UsernameVariable=null,string? PasswordVariable=null);
public sealed record RedisRequestSpec(
    string Command,
    string Key,
    string? Field=null,
    string? Value=null,
    string? ConnectionStringVariable=null,
    Dictionary<string,string>? Entries=null,
    string? Group=null,
    string? Consumer=null,
    string? PayloadField=null,
    Dictionary<string,string>? Filters=null,
    int? Count=null,
    int? BlockMs=null,
    bool? AckMatch=null,
    string? StartId=null);
public sealed record MqttRequestSpec(string Action,string? Topic,string? Payload,int? Qos,bool? Retain,int? TimeoutMs,string? Username,string? Password,string? ClientId,MqttWillSpec? Will);
public sealed record MqttWillSpec(string? Topic,string? Payload,int? Qos,bool? Retain);
public sealed record HttpStubRequestSpec(string Action,string? Method,string? Path,int? Status,JsonElement? Response,Dictionary<string,string>? ResponseHeaders,int? DelayMs,int? TimeoutMs);
public sealed record ExpectSpec(int? Status,int[]? StatusOneOf,int? MaxResponseTimeMs,Dictionary<string,AssertionSpec>? Json,MqttExpectSpec? Mqtt,DatabaseExpectSpec? Database,HttpStubExpectSpec? HttpStub = null,RedisExpectSpec? Redis = null,JsonElement? JsonSchema = null);
public sealed record RedisExpectSpec(string? ValueEquals=null,bool? Exists=null,long? TtlGreaterThan=null);
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
