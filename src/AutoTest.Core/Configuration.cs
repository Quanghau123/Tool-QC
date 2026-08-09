using System.Text.Json;
namespace AutoTest.Core;
public sealed class EnvironmentStore
{
 private readonly Dictionary<string,string> values=new(StringComparer.OrdinalIgnoreCase);
 public static EnvironmentStore Load(string path){var s=new EnvironmentStore();if(!File.Exists(path))return s;foreach(var raw in File.ReadLines(path)){var line=raw.Trim();if(line.Length==0||line.StartsWith('#'))continue;var i=line.IndexOf('=');if(i>0)s.values[line[..i].Trim()]=line[(i+1)..].Trim().Trim('"');}return s;}
 public string? Get(string key)=>Environment.GetEnvironmentVariable(key) is {Length:>0} v?v:values.GetValueOrDefault(key);
 public string Require(string key)=>Get(key) is {Length:>0} v?v:throw new InvalidOperationException($"Thiếu cấu hình bắt buộc: {key}");
 public bool Bool(string key,bool fallback=false)=>bool.TryParse(Get(key),out var v)?v:fallback;
 public int Int(string key,int fallback=0)=>int.TryParse(Get(key),out var v)?v:fallback;
}
public static class SpecLoader
{
 private static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,AllowTrailingCommas=true};
 public static ProjectSpec Project(string path)=>Read<ProjectSpec>(path);
 public static IEnumerable<CaseSpec> Cases(string dir,string project){foreach(var path in Directory.EnumerateFiles(dir,"*.json",SearchOption.AllDirectories)){var suite=Read<SuiteSpec>(path);if(!suite.Project.Equals(project,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException($"Project mismatch: {path}");foreach(var c in suite.Cases){if(string.IsNullOrWhiteSpace(c.Id)||c.Steps.Count==0)throw new InvalidDataException($"Invalid case: {path}");yield return c;}}}
 private static T Read<T>(string path)=>JsonSerializer.Deserialize<T>(File.ReadAllText(path),Options)??throw new InvalidDataException($"Invalid JSON: {path}");
}
