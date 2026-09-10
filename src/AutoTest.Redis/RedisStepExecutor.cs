using System.Diagnostics;
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
        string expected = Describe(context.Step.Expect?.Redis);
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
                _ => throw new InvalidDataException($"Không hỗ trợ Redis command: {request.Command}")
            };
            if (context.Assertions) await AssertAsync(database, key, actual, context.Step.Expect?.Redis);
            return Result(true, actual, null);
        }
        catch (Exception exception) { return Result(false, actual, context.Redact?.Invoke(exception.Message) ?? exception.Message); }

        StepRunResult Result(bool passed, string? response, string? error) => new(context.Step.Name, context.Cleanup, passed,
            $"REDIS {command.ToUpperInvariant()}", key, null, expected, null, response, watch.Elapsed, error);
    }

    private static string ResolveRequired(string? value, string message, StepExecutionContext context)
        => Templates.Resolve(value ?? throw new InvalidDataException(message), context.Variables, context.Environment);

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

    private static string Describe(RedisExpectSpec? expect) => expect is null ? "Không đối chiếu" : $"valueEquals={expect.ValueEquals}, exists={expect.Exists}, ttl>{expect.TtlGreaterThan}";
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
