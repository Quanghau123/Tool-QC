using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoTest.Abstractions;
using StackExchange.Redis;

namespace AutoTest.Redis;

public sealed class RedisStepExecutor : ITestStepExecutor
{
    public string Name => "redis";
    public bool CanExecute(StepSpec step) => step.Request?.Redis is not null;

    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        Stopwatch watch = Stopwatch.StartNew();
        RedisRequestSpec request = context.Step.Request!.Redis!;
        string key = Templates.Resolve(request.Key, context.Variables, context.Environment);
        string command = request.Command.Trim().ToLowerInvariant();
        string connectionVariable = request.ConnectionStringVariable ?? "REDIS_CONNECTION_STRING";
        string expected = Describe(context.Step.Expect?.Redis, context.Step.Expect?.Json);
        string? actual = null;
        try
        {
            ConfigurationOptions options = ConfigurationOptions.Parse(context.Environment.Require(connectionVariable));
            options.AbortOnConnectFail = false;
            await using ConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(options);
            IDatabase database = connection.GetDatabase();
            actual = command switch
            {
                "get" => (await database.StringGetAsync(key)).ToString(),
                "hget" => (await database.HashGetAsync(key, ResolveRequired(request.Field, "Redis hget yêu cầu field.", context))).ToString(),
                "set" => (await database.StringSetAsync(key, Templates.Resolve(request.Value ?? string.Empty, context.Variables, context.Environment))).ToString(),
                "delete" => (await database.KeyDeleteAsync(key)).ToString(),
                "exists" => (await database.KeyExistsAsync(key)).ToString(),
                "xadd" => (await database.StreamAddAsync(key, ResolveEntries(request, context))).ToString(),
                "xreadgroup" => await ReadMatchingStreamEntryAsync(database, key, request, context, cancellationToken),
                _ => throw new InvalidDataException($"Không hỗ trợ Redis command: {request.Command}")
            };
            if (context.Assertions)
            {
                await AssertAsync(database, key, actual, context.Step.Expect?.Redis);
                if (command == "xreadgroup") AssertAndSaveJson(context, actual);
            }
            return Result(true, actual, null);
        }
        catch (Exception exception)
        {
            return Result(false, actual, context.Redact?.Invoke(exception.Message) ?? exception.Message);
        }

        StepRunResult Result(bool passed, string? response, string? error) => new(context.Step.Name, context.Cleanup, passed,
            $"REDIS {command.ToUpperInvariant()}", key, null, expected, null, response, watch.Elapsed, error);
    }

    private static async Task<string> ReadMatchingStreamEntryAsync(
        IDatabase database,
        string streamKey,
        RedisRequestSpec request,
        StepExecutionContext context,
        CancellationToken cancellationToken)
    {
        string group = ResolveRequired(request.Group, "Redis xreadgroup yêu cầu group.", context);
        string consumer = ResolveRequired(request.Consumer, "Redis xreadgroup yêu cầu consumer.", context);
        string payloadField = ResolveRequired(request.PayloadField, "Redis xreadgroup yêu cầu payloadField.", context);
        RedisValue startId = ResolveStartId(request, context);
        int count = request.Count ?? 32;
        int blockMs = request.BlockMs ?? 5000;
        bool ackMatch = request.AckMatch ?? true;
        bool groupCreated = await EnsureConsumerGroupAsync(database, streamKey, group, startId);
        RedisValue position = groupCreated ? startId : StreamPosition.NewMessages;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(0, blockMs));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamEntry[] entries = await database.StreamReadGroupAsync(streamKey, group, consumer, position, count);
            position = StreamPosition.NewMessages;
            foreach (StreamEntry entry in entries)
            {
                if (!TryGetPayload(entry, payloadField, out string payload)) continue;
                using JsonDocument document = JsonDocument.Parse(payload);
                if (!MatchesFilters(document.RootElement, request.Filters, context)) continue;
                if (ackMatch) await database.StreamAcknowledgeAsync(streamKey, group, entry.Id);
                return document.RootElement.GetRawText();
            }
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException($"Không tìm thấy Redis Stream message khớp filter trong {blockMs}ms. Stream={streamKey}, Group={group}.");
            await Task.Delay(Math.Min(250, Math.Max(1, blockMs)), cancellationToken);
        }
    }

    private static RedisValue ResolveStartId(RedisRequestSpec request, StepExecutionContext context)
        => string.IsNullOrWhiteSpace(request.StartId) ? "0-0" : Templates.Resolve(request.StartId, context.Variables, context.Environment);

    private static async Task<bool> EnsureConsumerGroupAsync(IDatabase database, string streamKey, string group, RedisValue startId)
    {
        try
        {
            await database.StreamCreateConsumerGroupAsync(streamKey, group, startId, createStream: true);
            return true;
        }
        catch (RedisServerException exception) when (exception.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }
    private static bool TryGetPayload(StreamEntry entry, string payloadField, out string payload)
    {
        foreach (NameValueEntry field in entry.Values)
            if (field.Name.ToString().Equals(payloadField, StringComparison.Ordinal))
            {
                payload = field.Value.ToString();
                return !string.IsNullOrWhiteSpace(payload);
            }
        payload = string.Empty;
        return false;
    }

    private static bool MatchesFilters(JsonElement payload, Dictionary<string, string>? filters, StepExecutionContext context)
    {
        foreach ((string path, string expected) in filters ?? [])
        {
            var selected = JsonPath.Select(payload, path);
            string wanted = Templates.Resolve(expected, context.Variables, context.Environment);
            if (!selected.Found || !string.Equals(JsonPath.Text(selected.Value), wanted, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static void AssertAndSaveJson(StepExecutionContext context, string payload)
    {
        if ((context.Step.Expect?.Json?.Count ?? 0) == 0 && context.Step.Save is null) return;
        using JsonDocument document = JsonDocument.Parse(payload);
        foreach ((string path, AssertionSpec rule) in context.Step.Expect?.Json ?? [])
        {
            IReadOnlyList<JsonElement> values = JsonPath.SelectMany(document.RootElement, path);
            if (rule.Exists is bool exists && (values.Count > 0) != exists)
                throw new InvalidOperationException($"Trường {path} không đúng yêu cầu về sự tồn tại.");
            if (values.Count == 0) continue;
            foreach (JsonElement value in values) AssertValue(context, path, value, rule);
        }
        foreach ((string name, string path) in context.Step.Save ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, path);
            if (!selected.Found) throw new InvalidOperationException($"Không thể lưu biến {name} từ payload Redis Stream.");
            context.Variables[name] = JsonPath.Text(selected.Value);
        }
    }

    private static void AssertValue(StepExecutionContext context, string path, JsonElement value, AssertionSpec rule)
    {
        string actual = JsonPath.Text(value);
        string Resolve(JsonElement element) => Templates.Resolve(JsonPath.Text(element), context.Variables, context.Environment);
        if (rule.ExpectedValue is JsonElement expected && !JsonAssertionValues.Equal(value, expected, Resolve(expected)))
            throw new InvalidOperationException($"Giá trị của trường {path} không đúng như mong đợi. Thực tế: {actual}.");
        if (rule.NotEquals is JsonElement notExpected && JsonAssertionValues.Equal(value, notExpected, Resolve(notExpected)))
            throw new InvalidOperationException($"Giá trị của trường {path} không được bằng {actual}.");
        if (rule.Contains is string contains && !actual.Contains(Templates.Resolve(contains, context.Variables, context.Environment), StringComparison.Ordinal))
            throw new InvalidOperationException($"Trường {path} không chứa nội dung mong đợi.");
        if (rule.Matches is string pattern && !Regex.IsMatch(actual, Templates.Resolve(pattern, context.Variables, context.Environment), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new InvalidOperationException($"Trường {path} không khớp biểu thức mong đợi.");
        if (rule.OneOf is { Length: > 0 } options && !options.Any(option => JsonAssertionValues.Equal(value, option, Resolve(option))))
            throw new InvalidOperationException($"Trường {path} không thuộc tập giá trị mong đợi.");
        if (rule.GreaterThan is not null || rule.GreaterThanOrEqual is not null || rule.LessThan is not null || rule.LessThanOrEqual is not null)
        {
            if (!decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                throw new InvalidOperationException($"Trường {path} không phải số để so sánh.");
            if (rule.GreaterThan is decimal greaterThan && number <= greaterThan) throw new InvalidOperationException($"Trường {path} phải lớn hơn {greaterThan}.");
            if (rule.GreaterThanOrEqual is decimal greaterThanOrEqual && number < greaterThanOrEqual) throw new InvalidOperationException($"Trường {path} phải lớn hơn hoặc bằng {greaterThanOrEqual}.");
            if (rule.LessThan is decimal lessThan && number >= lessThan) throw new InvalidOperationException($"Trường {path} phải nhỏ hơn {lessThan}.");
            if (rule.LessThanOrEqual is decimal lessThanOrEqual && number > lessThanOrEqual) throw new InvalidOperationException($"Trường {path} phải nhỏ hơn hoặc bằng {lessThanOrEqual}.");
        }
    }

    private static string ResolveRequired(string? value, string message, StepExecutionContext context)
        => Templates.Resolve(value ?? throw new InvalidDataException(message), context.Variables, context.Environment);

    private static NameValueEntry[] ResolveEntries(RedisRequestSpec request, StepExecutionContext context)
    {
        if (request.Entries is null || request.Entries.Count == 0) throw new InvalidDataException("Redis xadd yêu cầu entries.");
        return request.Entries.Select(entry => new NameValueEntry(entry.Key, Templates.Resolve(entry.Value, context.Variables, context.Environment))).ToArray();
    }

    private static async Task AssertAsync(IDatabase database, string key, string actual, RedisExpectSpec? expect)
    {
        if (expect is null) return;
        if (expect.ValueEquals is { } value && !string.Equals(actual, value, StringComparison.Ordinal)) throw new InvalidOperationException($"Giá trị Redis không đúng. Thực tế: {actual}");
        if (expect.Exists is { } exists && await database.KeyExistsAsync(key) != exists) throw new InvalidOperationException($"Trạng thái tồn tại của Redis key {key} không đúng.");
        if (expect.TtlGreaterThan is { } threshold)
        {
            TimeSpan? ttl = await database.KeyTimeToLiveAsync(key);
            if (ttl is null || ttl.Value.TotalSeconds <= threshold) throw new InvalidOperationException($"TTL của Redis key {key} không đạt yêu cầu.");
        }
    }

    private static string Describe(RedisExpectSpec? expect, Dictionary<string, AssertionSpec>? json)
        => expect is null && (json?.Count ?? 0) == 0 ? "Không đối chiếu" : $"redis={expect is not null}, json={json?.Count ?? 0}";

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
