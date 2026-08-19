using AutoTest.Abstractions;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Globalization;
using System.Text.RegularExpressions;
namespace AutoTest.Http;

/// <summary>HTTP module marker and executor boundary. The compatibility engine currently delegates here incrementally.</summary>
public sealed class HttpStepExecutor : ITestStepExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
    private readonly HttpClient client;
    public HttpStepExecutor(ProjectSpec project, IEnvironmentStore environment)
    {
        string url = environment.Require(project.BaseUrlVariable);
        client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(environment.Int("API_TIMEOUT_SECONDS", 30)) };
        foreach (var header in project.DefaultHeaders ?? []) client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }
    public string Name => "http";
    public bool CanExecute(StepSpec step) => step.Request is { Database: null, Mqtt: null };
    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        StepSpec step=context.Step; RequestSpec request=step.Request!; var watch=Stopwatch.StartNew();
        string method=request.Method??throw new InvalidDataException($"Thiếu phương thức HTTP cho bước: {step.Name}");
        string path=Templates.Resolve(request.Path??throw new InvalidDataException($"Thiếu đường dẫn HTTP cho bước: {step.Name}"),context.Variables,context.Environment);
        string? payload=request.Body is { } body?Templates.Resolve(body.GetRawText(),context.Variables,context.Environment):null;
        Dictionary<string,string>? form=request.Form?.ToDictionary(x=>x.Key,x=>Templates.Resolve(x.Value,context.Variables,context.Environment));
        string? reportPayload=payload??(form is null?null:JsonSerializer.Serialize(form)); int? status=null; string? responseText=null;
        string expected=DescribeExpected(step.Expect,context);
        try {
            using var message=new HttpRequestMessage(new HttpMethod(method),path);
            if(!string.IsNullOrWhiteSpace(step.Auth)) {
                string? token=!string.IsNullOrWhiteSpace(step.AuthToken)?Templates.Resolve(step.AuthToken,context.Variables,context.Environment):await (context.ResolveAuthenticationToken?.Invoke(cancellationToken)??Task.FromResult<string?>(null));
                if(token is not null) message.Headers.Authorization=new AuthenticationHeaderValue(context.Project.Authentication?.Prefix??"Bearer",token);
            }
            foreach(var header in request.Headers??[]) message.Headers.TryAddWithoutValidation(header.Key,Templates.Resolve(header.Value,context.Variables,context.Environment));
            if(payload is not null&&form is not null) throw new InvalidDataException($"Bước '{step.Name}' không thể gửi đồng thời body JSON và form.");
            if(payload is not null) message.Content=new StringContent(payload,Encoding.UTF8,"application/json");
            else if(form is not null){var content=new MultipartFormDataContent();foreach(var item in form)content.Add(new StringContent(item.Value),item.Key);message.Content=content;}
            using var response=await client.SendAsync(message,cancellationToken);status=(int)response.StatusCode;responseText=await response.Content.ReadAsStringAsync(cancellationToken);
            if(context.Cleanup&&!response.IsSuccessStatusCode)throw new InvalidOperationException($"Bước dọn dữ liệu nhận mã HTTP {status}. Nội dung phản hồi: {R(context,responseText)}");
            if(context.Assertions){ExpectSpec exp=step.Expect??throw new InvalidDataException($"Thiếu cấu hình kết quả mong đợi cho bước: {step.Name}");int[] accepted=exp.StatusOneOf??(exp.Status is { } s?[s]:[]);if(accepted.Length==0)throw new InvalidDataException($"Thiếu status hoặc statusOneOf cho bước: {step.Name}");if(!accepted.Contains(status.Value))throw new InvalidOperationException($"{step.Name}: mong đợi mã HTTP thuộc [{string.Join(", ",accepted)}], thực tế nhận {status}. Nội dung phản hồi: {R(context,responseText)}");if(exp.MaxResponseTimeMs is { } max&&watch.ElapsedMilliseconds>max)throw new InvalidOperationException($"Thời gian phản hồi vượt quá giới hạn {max} ms.");AssertAndSave(step,exp,responseText,context);}
            return new(step.Name,context.Cleanup,true,method,path,Sanitize(reportPayload,context),expected,status,Sanitize(responseText,context),watch.Elapsed,null);
        } catch(Exception ex){return new(step.Name,context.Cleanup,false,method,path,Sanitize(reportPayload,context),expected,status,Sanitize(responseText,context),watch.Elapsed,R(context,ex.Message));}
    }
    private static void AssertAndSave(StepSpec step,ExpectSpec expected,string body,StepExecutionContext c){if((expected.Json?.Count??0)==0&&step.Save is null)return;using var doc=JsonDocument.Parse(body);foreach(var entry in expected.Json??[]){IReadOnlyList<JsonElement> values=JsonPath.SelectMany(doc.RootElement,entry.Key);AssertionSpec rule=entry.Value;if(rule.Exists is { } exists&&(values.Count>0)!=exists)throw new InvalidOperationException($"Trường {entry.Key} không đúng yêu cầu về sự tồn tại.");if(values.Count==0)continue;if(rule.Count is { } count){JsonElement first=values[0];int actualCount=first.ValueKind switch{JsonValueKind.Array=>first.GetArrayLength(),JsonValueKind.Object=>first.EnumerateObject().Count(),_=>values.Count};if(actualCount!=count)throw new InvalidOperationException($"Số phần tử của trường {entry.Key} mong đợi {count}, thực tế {actualCount}.");}foreach(JsonElement selected in values)AssertValue(entry.Key,selected,rule,c);}foreach(var saved in step.Save??[]){var selected=JsonPath.Select(doc.RootElement,saved.Value);if(!selected.Found)throw new InvalidOperationException($"Không thể lưu biến {saved.Key} từ phản hồi.");c.Variables[saved.Key]=JsonPath.Text(selected.Value);}}
    private static void AssertValue(string path,JsonElement value,AssertionSpec rule,StepExecutionContext c){string actual=JsonPath.Text(value);string Resolve(JsonElement x)=>Templates.Resolve(JsonPath.Text(x),c.Variables,c.Environment);if(rule.ExpectedValue is { } expected&&actual!=Resolve(expected))throw new InvalidOperationException($"Giá trị của trường {path} không đúng như mong đợi.");if(rule.NotEquals is { } notExpected&&actual==Resolve(notExpected))throw new InvalidOperationException($"Giá trị của trường {path} không được bằng {actual}.");if(rule.Contains is { } contains&&!actual.Contains(Templates.Resolve(contains,c.Variables,c.Environment),StringComparison.Ordinal))throw new InvalidOperationException($"Trường {path} không chứa nội dung mong đợi.");if(rule.Matches is { } pattern&&!Regex.IsMatch(actual,Templates.Resolve(pattern,c.Variables,c.Environment),RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)))throw new InvalidOperationException($"Trường {path} không khớp biểu thức mong đợi.");if(rule.OneOf is { Length:>0 } options&&!options.Select(Resolve).Contains(actual,StringComparer.Ordinal))throw new InvalidOperationException($"Trường {path} không thuộc tập giá trị mong đợi.");if(rule.Type is { } type&&!TypeMatches(value,type))throw new InvalidOperationException($"Kiểu của trường {path} không phải {type}.");if(rule.GreaterThan is not null||rule.GreaterThanOrEqual is not null||rule.LessThan is not null||rule.LessThanOrEqual is not null){if(!decimal.TryParse(actual,NumberStyles.Number,CultureInfo.InvariantCulture,out decimal number))throw new InvalidOperationException($"Trường {path} không phải số để so sánh.");if(rule.GreaterThan is { } gt&&number<=gt)throw new InvalidOperationException($"Trường {path} phải lớn hơn {gt}.");if(rule.GreaterThanOrEqual is { } gte&&number<gte)throw new InvalidOperationException($"Trường {path} phải lớn hơn hoặc bằng {gte}.");if(rule.LessThan is { } lt&&number>=lt)throw new InvalidOperationException($"Trường {path} phải nhỏ hơn {lt}.");if(rule.LessThanOrEqual is { } lte&&number>lte)throw new InvalidOperationException($"Trường {path} phải nhỏ hơn hoặc bằng {lte}.");}}
    private static bool TypeMatches(JsonElement value,string type)=>type.ToLowerInvariant() switch{"string"=>value.ValueKind==JsonValueKind.String,"number"=>value.ValueKind==JsonValueKind.Number,"integer"=>value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out _),"boolean" or "bool"=>value.ValueKind is JsonValueKind.True or JsonValueKind.False,"array"=>value.ValueKind==JsonValueKind.Array,"object"=>value.ValueKind==JsonValueKind.Object,"null"=>value.ValueKind==JsonValueKind.Null,_=>throw new InvalidDataException($"Kiểu assertion không hỗ trợ: {type}")};
    private static string DescribeExpected(ExpectSpec? e,StepExecutionContext c){if(!c.Assertions)return c.Cleanup?"Bước dọn dữ liệu: không đối chiếu kết quả":"Không đối chiếu";if(e is null)return "Thiếu cấu hình kết quả mong đợi";var lines=new List<string>();if(e.Status is { } s)lines.Add($"Mã trạng thái HTTP = {s}");if(e.StatusOneOf is { Length:>0 } ss)lines.Add($"Mã trạng thái HTTP thuộc [{string.Join(", ",ss)}]");if(e.MaxResponseTimeMs is { } max)lines.Add($"Thời gian phản hồi không quá {max} ms");foreach(var item in e.Json??[]){var rules=new List<string>();if(item.Value.Exists is { } x)rules.Add(x?"tồn tại":"không tồn tại");if(item.Value.ExpectedValue is { } v)rules.Add($"bằng {Templates.Resolve(JsonPath.Text(v),c.Variables,c.Environment)}");if(item.Value.Contains is { } ct)rules.Add($"chứa {Templates.Resolve(ct,c.Variables,c.Environment)}");lines.Add($"{item.Key}: {string.Join(", ",rules)}");}return string.Join("\n",lines);}
    private static string? Sanitize(string? value,StepExecutionContext c){if(string.IsNullOrWhiteSpace(value))return value;try{using var doc=JsonDocument.Parse(value);return JsonSerializer.Serialize(SanitizeElement(doc.RootElement,c),JsonOptions);}catch{return R(c,value);}}
    private static object? SanitizeElement(JsonElement e,StepExecutionContext c)=>e.ValueKind switch{JsonValueKind.Object=>e.EnumerateObject().ToDictionary(p=>p.Name,p=>Sensitive(p.Name)?(object?)"***":SanitizeElement(p.Value,c)),JsonValueKind.Array=>e.EnumerateArray().Select(x=>SanitizeElement(x,c)).ToArray(),JsonValueKind.String=>R(c,e.GetString()??""),JsonValueKind.Number=>e.TryGetInt64(out long n)?n:e.GetDouble(),JsonValueKind.True=>true,JsonValueKind.False=>false,_=>null};
    private static bool Sensitive(string n)=>new[]{"password","confirmPassword","token","accessToken","refreshToken","authorization","secret","connectionString"}.Any(x=>n.Contains(x,StringComparison.OrdinalIgnoreCase));
    private static string R(StepExecutionContext c,string value)=>c.Redact?.Invoke(value)??value;
    public ValueTask DisposeAsync(){client.Dispose();return ValueTask.CompletedTask;}
}


