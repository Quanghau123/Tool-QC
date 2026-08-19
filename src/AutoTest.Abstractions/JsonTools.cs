using System.Text.Json;using System.Text.RegularExpressions;using AutoTest.Abstractions;
namespace AutoTest.Abstractions;
public static partial class Templates
{
 [GeneratedRegex(@"\$\{(?<name>[A-Za-z0-9_.:-]+)\}")]private static partial Regex Pattern();
 public static string Resolve(string input,IReadOnlyDictionary<string,string> vars,IEnvironmentStore env)=>Pattern().Replace(input,m=>{var key=m.Groups["name"].Value;if(key.StartsWith("env:",StringComparison.OrdinalIgnoreCase))return env.Require(key[4..]);return vars.TryGetValue(key,out var v)?v:throw new InvalidOperationException($"Không tìm thấy biến: {key}");});
 public static IReadOnlyList<string> Variables(string input)=>Pattern().Matches(input).Select(match=>match.Groups["name"].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
public static class JsonPath
{
public static (bool Found,JsonElement Value) Select(JsonElement root,string path){var values=SelectMany(root,path);return values.Count==0?(false,default):(true,values[0]);}
public static IReadOnlyList<JsonElement> SelectMany(JsonElement root,string path){if(path=="$")return[root];if(!path.StartsWith("$.",StringComparison.Ordinal))throw new InvalidOperationException($"JSON path không hợp lệ: {path}");IReadOnlyList<JsonElement> current=[root];foreach(string part in path[2..].Split('.')){var next=new List<JsonElement>();foreach(JsonElement item in current){if(part=="*"&&item.ValueKind==JsonValueKind.Array){next.AddRange(item.EnumerateArray());continue;}if(item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(part,out JsonElement property)){next.Add(property);continue;}if(item.ValueKind==JsonValueKind.Array&&int.TryParse(part,out int index)&&index>=0&&index<item.GetArrayLength())next.Add(item[index]);}current=next;if(current.Count==0)break;}return current;}
 public static string Text(JsonElement v)=>v.ValueKind==JsonValueKind.String?v.GetString()!:v.GetRawText();
}



