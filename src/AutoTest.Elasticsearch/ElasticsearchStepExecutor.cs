using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using AutoTest.Abstractions;

namespace AutoTest.Elasticsearch;

public sealed class ElasticsearchStepExecutor : ITestStepExecutor
{
    public string Name => "elasticsearch";
    public bool CanExecute(StepSpec step) => step.Request?.Elasticsearch is not null;

    public static string ResolvePath(ElasticsearchRequestSpec request, StepExecutionContext context)
        => Templates.Resolve(request.Path, context.Variables, context.Environment);

    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        ElasticsearchRequestSpec request = context.Step.Request!.Elasticsearch!;
        string urlVariable = string.IsNullOrWhiteSpace(request.UrlVariable) ? "ELASTICSEARCH_URL" : request.UrlVariable;
        string method = Templates.Resolve(request.Method, context.Variables, context.Environment);
        string path = ResolvePath(request, context);
        string expected = context.Step.Expect?.Status is int status ? $"HTTP {status}" : "HTTP thành công";
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(context.Environment.Require(urlVariable)) };
            string usernameVariable = string.IsNullOrWhiteSpace(request.UsernameVariable) ? "ELASTICSEARCH_USERNAME" : request.UsernameVariable;
            string passwordVariable = string.IsNullOrWhiteSpace(request.PasswordVariable) ? "ELASTICSEARCH_PASSWORD" : request.PasswordVariable;
            string? username = context.Environment.Get(usernameVariable);
            string? password = context.Environment.Get(passwordVariable);
            if (!string.IsNullOrWhiteSpace(username))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

            using var message = new HttpRequestMessage(new HttpMethod(method), path);
            string? payload = request.Body is JsonElement body
                ? Templates.Resolve(body.GetRawText(), context.Variables, context.Environment)
                : null;
            if (payload is not null) message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (context.Assertions && context.Step.Expect?.Status is int wanted && (int)response.StatusCode != wanted)
                throw new InvalidOperationException($"{context.Step.Name}: mong đợi HTTP {wanted}, thực tế {(int)response.StatusCode}. Body: {responseBody}");
            if (context.Assertions) AssertAndSave(context, responseBody);
            return new(context.Step.Name, context.Cleanup, true, method, path, payload, expected, (int)response.StatusCode, responseBody, watch.Elapsed, null);
        }
        catch (Exception exception)
        {
            return new(context.Step.Name, context.Cleanup, false, method, path, null, expected, null, null, watch.Elapsed, context.Redact?.Invoke(exception.Message) ?? exception.Message);
        }
    }

    private static void AssertAndSave(StepExecutionContext context, string body)
    {
        ExpectSpec? expected = context.Step.Expect;
        if ((expected?.Json?.Count ?? 0) == 0 && context.Step.Save is null) return;
        using var document = JsonDocument.Parse(body);
        foreach ((string path, AssertionSpec rule) in expected?.Json ?? [])
        {
            IReadOnlyList<JsonElement> values = JsonPath.SelectMany(document.RootElement, path);
            if (rule.Exists is bool exists && (values.Count > 0) != exists)
                throw new InvalidOperationException($"Trường {path} không đúng yêu cầu về sự tồn tại.");
            if (values.Count == 0) continue;
            if (rule.Count is int count)
            {
                JsonElement first = values[0];
                int actualCount = first.ValueKind switch { JsonValueKind.Array => first.GetArrayLength(), JsonValueKind.Object => first.EnumerateObject().Count(), _ => values.Count };
                if (actualCount != count) throw new InvalidOperationException($"Số phần tử của trường {path} mong đợi {count}, thực tế {actualCount}.");
            }
            foreach (JsonElement value in values) AssertValue(context, path, value, rule);
        }
        foreach ((string name, string path) in context.Step.Save ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, path);
            if (!selected.Found) throw new InvalidOperationException($"Không thể lưu biến {name} từ phản hồi Elasticsearch.");
            context.Variables[name] = JsonPath.Text(selected.Value);
        }
    }

    private static void AssertValue(StepExecutionContext context, string path, JsonElement value, AssertionSpec rule)
    {
        string actual = JsonPath.Text(value);
        string Resolve(JsonElement element) => Templates.Resolve(JsonPath.Text(element), context.Variables, context.Environment);
        if (rule.ExpectedValue is JsonElement expected && !JsonAssertionValues.Equal(value, expected, Resolve(expected))) throw new InvalidOperationException($"Giá trị của trường {path} không đúng như mong đợi. Thực tế: {actual}.");
        if (rule.NotEquals is JsonElement notExpected && JsonAssertionValues.Equal(value, notExpected, Resolve(notExpected))) throw new InvalidOperationException($"Giá trị của trường {path} không được bằng {actual}.");
        if (rule.Contains is string contains && !actual.Contains(Templates.Resolve(contains, context.Variables, context.Environment), StringComparison.Ordinal)) throw new InvalidOperationException($"Trường {path} không chứa nội dung mong đợi.");
        if (rule.Matches is string pattern && !Regex.IsMatch(actual, Templates.Resolve(pattern, context.Variables, context.Environment), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) throw new InvalidOperationException($"Trường {path} không khớp biểu thức mong đợi.");
        if (rule.OneOf is { Length: > 0 } options && !options.Any(option => JsonAssertionValues.Equal(value, option, Resolve(option)))) throw new InvalidOperationException($"Trường {path} không thuộc tập giá trị mong đợi.");
        if (rule.GreaterThan is not null || rule.GreaterThanOrEqual is not null || rule.LessThan is not null || rule.LessThanOrEqual is not null)
        {
            if (!decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)) throw new InvalidOperationException($"Trường {path} không phải số để so sánh.");
            if (rule.GreaterThan is decimal greaterThan && number <= greaterThan) throw new InvalidOperationException($"Trường {path} phải lớn hơn {greaterThan}.");
            if (rule.GreaterThanOrEqual is decimal greaterThanOrEqual && number < greaterThanOrEqual) throw new InvalidOperationException($"Trường {path} phải lớn hơn hoặc bằng {greaterThanOrEqual}.");
            if (rule.LessThan is decimal lessThan && number >= lessThan) throw new InvalidOperationException($"Trường {path} phải nhỏ hơn {lessThan}.");
            if (rule.LessThanOrEqual is decimal lessThanOrEqual && number > lessThanOrEqual) throw new InvalidOperationException($"Trường {path} phải nhỏ hơn hoặc bằng {lessThanOrEqual}.");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
