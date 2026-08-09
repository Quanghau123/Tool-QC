using System.Text.Json;
using System.Text.Json.Serialization;
namespace AutoTest.Core;
public sealed record ProjectSpec(string Name,string BaseUrlVariable,Dictionary<string,string>? DefaultHeaders,AuthSpec? Authentication,SafetySpec? Safety);
public sealed record AuthSpec(string Strategy,string? LoginPath,string? Method,JsonElement? Body,string? TokenPath,string? Header,string? Prefix);
public sealed record SafetySpec(string[]? ProductionHosts);
public sealed record SuiteSpec(string Project,List<CaseSpec> Cases);
public sealed record CaseSpec(string Id,string Name,string[]? Tags,bool Destructive,Dictionary<string,string>? Variables,List<StepSpec> Steps,List<StepSpec>? Cleanup);
public sealed record StepSpec(string Name,string? Auth,RequestSpec Request,ExpectSpec? Expect,Dictionary<string,string>? Save);
public sealed record RequestSpec(string Method,string Path,JsonElement? Body,Dictionary<string,string>? Headers);
public sealed record ExpectSpec(int Status,int? MaxResponseTimeMs,Dictionary<string,AssertionSpec>? Json);
public sealed class AssertionSpec
{
 [JsonPropertyName("equals")]
 public JsonElement? ExpectedValue { get; init; }
 public bool? Exists { get; init; }
 public string? Type { get; init; }
 public string? Contains { get; init; }
}
public sealed record RunResult(string Id,string Name,bool Passed,TimeSpan Duration,string? Error);
