using System.Text.Json;using System.Text.RegularExpressions;
namespace AutoTest.Core;
public static partial class Templates
{
 [GeneratedRegex(@"\$\{(?<name>[A-Za-z0-9_.:-]+)\}")]private static partial Regex Pattern();
 public static string Resolve(string input,IReadOnlyDictionary<string,string> vars,EnvironmentStore env)=>Pattern().Replace(input,m=>{var key=m.Groups["name"].Value;if(key.StartsWith("env:",StringComparison.OrdinalIgnoreCase))return env.Require(key[4..]);return vars.TryGetValue(key,out var v)?v:throw new InvalidOperationException($"Không tìm thấy biến: {key}");});
}
public static class JsonPath
{
public static (bool Found,JsonElement Value) Select(JsonElement root,string path){if(path=="$")return(true,root);if(!path.StartsWith("$.",StringComparison.Ordinal))throw new InvalidOperationException($"Unsupported JSON path: {path}");var current=root;foreach(var part in path[2..].Split('.')){if(current.ValueKind==JsonValueKind.Object&&current.TryGetProperty(part,out current))continue;if(current.ValueKind==JsonValueKind.Array&&int.TryParse(part,out var index)&&index>=0&&index<current.GetArrayLength()){current=current[index];continue;}return(false,default);}return(true,current);}
 public static string Text(JsonElement v)=>v.ValueKind==JsonValueKind.String?v.GetString()!:v.GetRawText();
}
