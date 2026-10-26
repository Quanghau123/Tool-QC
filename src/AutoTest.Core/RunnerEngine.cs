using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoTest.Http;
using AutoTest.PostgreSql;
using AutoTest.Elasticsearch;
using AutoTest.Mqtt;
using AutoTest.HttpStub;
using AutoTest.Redis;

namespace AutoTest.Core;

// Nhóm điều phối chính: lỗi thứ tự step, biến, auth, retry hoặc cleanup bắt đầu kiểm tra từ đây.
public sealed class RunnerEngine : IDisposable
{
    private readonly ProjectSpec project;
    private readonly EnvironmentStore env;
    private readonly ErrorCatalogueSpec? errorCatalogue;
    private readonly HttpClient http;
    private readonly StepExecutorRegistry executorRegistry;
    private string? token;

    // Khởi tạo các transport dùng chung và chặn URL không an toàn trước khi chạy.
    public RunnerEngine(
        ProjectSpec project,
        EnvironmentStore env,
        ErrorCatalogueSpec? errorCatalogue = null)
    {
        this.project = project;
        this.env = env;
        this.errorCatalogue = errorCatalogue;
        var url = env.Require(project.BaseUrlVariable);
        Guard(url);
        http = new HttpClient
        {
            BaseAddress = new Uri(url),
            Timeout = TimeSpan.FromSeconds(int.TryParse(env.Get("API_TIMEOUT_SECONDS"), out var seconds) ? seconds : 30)
        };
        foreach (var header in project.DefaultHeaders ?? [])
            http.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        executorRegistry = new StepExecutorRegistry(new ITestStepExecutor[]
        {
            new HttpStepExecutor(project, env),
            new PostgreSqlStepExecutor(),
            new ElasticsearchStepExecutor(),
            new MqttStepExecutor(env),
            new HttpStubStepExecutor(env),
            new RedisStepExecutor(),
        });
    }

    // Chạy trọn một testcase và gom kết quả từng bước cho báo cáo.
    public Task<RunResult> RunAsync(CaseSpec test, CancellationToken cancellationToken = default)
        => RunAsync(test, null, cancellationToken);

    // Chế độ chuỗi dùng chung biến đã save để testcase sau nhận dữ liệu từ testcase trước.
    public async Task<RunResult> RunAsync(CaseSpec test, IDictionary<string, string>? sharedVariables, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var started = false;
        var stepResults = new List<StepRunResult>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unique"] = Guid.NewGuid().ToString("N"),
            ["shortUniqueA"] = Guid.NewGuid().ToString("N")[..6],
            ["shortUniqueB"] = Guid.NewGuid().ToString("N")[..6],
            ["shortUniqueC"] = Guid.NewGuid().ToString("N")[..6],
            ["timestamp"] = now.ToUnixTimeSeconds().ToString(),
            ["timestampMs"] = now.ToUnixTimeMilliseconds().ToString(),
            ["nowIso"] = ToUtcIso(now),
            ["pastStartIso"] = ToUtcIso(now.AddHours(-1)),
            ["futureStartIso"] = ToUtcIso(now.AddHours(1)),
            ["futureEndIso"] = ToUtcIso(now.AddHours(2)),
            ["futureDay1Iso"] = ToUtcIso(now.AddDays(1)),
            ["futureDay4Iso"] = ToUtcIso(now.AddDays(4)),
            ["futureDay5Iso"] = ToUtcIso(now.AddDays(5)),
            ["futureDay6Iso"] = ToUtcIso(now.AddDays(6)),
            ["futureDay8Iso"] = ToUtcIso(now.AddDays(8)),
            ["futureDay9Iso"] = ToUtcIso(now.AddDays(9)),
            ["futureDay10Iso"] = ToUtcIso(now.AddDays(10)),
            ["futureDay15Iso"] = ToUtcIso(now.AddDays(15))
        };
        for (int index = 1; index <= 32; index++)
            variables[$"guid{index}"] = Guid.NewGuid().ToString();
        foreach ((string key, string value) in sharedVariables ?? new Dictionary<string, string>())
            variables[key] = value;

        try
        {
            if (test.Destructive && !env.Bool("ALLOW_DESTRUCTIVE_TESTS"))
                throw new InvalidOperationException("Kịch bản có thay đổi dữ liệu đang bị chặn. Hãy bật ALLOW_DESTRUCTIVE_TESTS trên môi trường kiểm thử an toàn.");
            foreach (var variable in test.Variables ?? [])
                variables[variable.Key] = ResolveTemplate(variable.Value, variables, env);
            started = true;
            foreach (var step in test.Steps)
                await RunConfiguredStepAsync(step, variables, cancellationToken, stepResults);
            if (variables.TryGetValue("authToken", out string? chainedToken) && !string.IsNullOrWhiteSpace(chainedToken))
                token = chainedToken;
            if (sharedVariables is not null)
                foreach ((string key, string value) in variables)
                    sharedVariables[key] = value;
            return new(test.Id, test.Name, true, watch.Elapsed, null, stepResults);
        }
        catch (Exception exception)
        {
            return new(test.Id, test.Name, false, watch.Elapsed, Redact(exception.Message), stepResults);
        }
        finally
        {
            if (started && test.Cleanup is { Count: > 0 })
                Console.WriteLine($"[GIỮ DỮ LIỆU] {test.Id}: bỏ qua {test.Cleanup.Count} bước cleanup để phục vụ kiểm tra lại.");
        }
    }

    // Chọn đúng executor; lỗi transport hoặc assertion của một step hội tụ tại đây.
    private async Task RunStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results, bool assertions = true, bool cleanup = false)
    {
        if (step.Request is null)
            throw new InvalidDataException($"Thiếu request cho bước: {step.Name}");
        var context = new StepExecutionContext(step, project, env, variables, assertions, cleanup,
            result => results.Add(result), ResolveAuthenticationTokenAsync, Redact, errorCatalogue);
        StepRunResult moduleResult = await executorRegistry.Resolve(step).ExecuteAsync(context, cancellationToken);
        results.Add(moduleResult);
        if (!moduleResult.Passed) throw new InvalidOperationException(moduleResult.Error ?? $"Bước thất bại: {step.Name}");
    }

    private async Task<string?> ResolveAuthenticationTokenAsync(CancellationToken cancellationToken)
    {
        await AuthenticateAsync(cancellationToken);
        return token;
    }

    private async Task RunConfiguredStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results)
    {
        if (step.ConcurrentRequests is { Count: > 0 })
        {
            if (step.Request is not null || step.ParallelRequests is not null || step.Retry is not null || step.Save is not null)
                throw new InvalidDataException($"Bước '{step.Name}' dùng concurrentRequests không được khai báo request, parallelRequests, retry hoặc save ở cấp bước.");
            if (step.ConcurrentRequests.Count > 1000)
                throw new InvalidDataException($"Bước '{step.Name}' hỗ trợ tối đa 1000 request đồng thời.");
            var requests = step.ConcurrentRequests.Select((item, index) =>
            {
                var requestStep = new StepSpec(
                    $"{step.Name} — {item.Name} ({index + 1}/{step.ConcurrentRequests.Count})",
                    item.Auth,
                    item.AuthToken,
                    item.Request,
                    item.Expect,
                    null,
                    null,
                    null,
                    null);
                var requestResults = new List<StepRunResult>();
                return RunStepAsync(
                    requestStep,
                    new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase),
                    cancellationToken,
                    requestResults).ContinueWith(task =>
                    {
                        if (task.IsFaulted) throw task.Exception!.InnerException ?? task.Exception;
                        return requestResults.Single();
                    }, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }).ToArray();
            try
            {
                results.AddRange(await Task.WhenAll(requests));
            }
            catch
            {
                foreach (var request in requests.Where(x => x.IsCompletedSuccessfully))
                    results.Add(request.Result);
                throw;
            }
            return;
        }
        if (step.ConcurrentRequests is { Count: 0 })
            throw new InvalidDataException($"concurrentRequests của bước '{step.Name}' không được để rỗng.");
        if (step.Request is null)
            throw new InvalidDataException($"Thiếu request cho bước: {step.Name}");
        int parallelRequests = step.ParallelRequests ?? 1;
        if (parallelRequests < 1 || parallelRequests > 1000)
            throw new InvalidDataException($"Số request đồng thời của bước '{step.Name}' phải từ 1 đến 1000.");
        if (parallelRequests == 1)
        {
            await RunStepWithRetryAsync(step, variables, cancellationToken, results);
            return;
        }
        if (step.Save is not null)
            throw new InvalidDataException($"Bước '{step.Name}' không thể vừa gửi song song vừa lưu biến từ phản hồi.");
        if (step.Retry is not null)
            throw new InvalidDataException($"Bước '{step.Name}' không thể dùng đồng thời retry và parallelRequests.");
        if (step.Request.Mqtt is not null)
            throw new InvalidDataException($"Bước '{step.Name}' chỉ hỗ trợ parallelRequests cho HTTP.");
        if (step.Request.Database is not null)
            throw new InvalidDataException($"Bước '{step.Name}' không hỗ trợ parallelRequests cho PostgreSQL.");

        var attempts = Enumerable.Range(1, parallelRequests).Select(async attempt =>
        {
            var attemptResults = new List<StepRunResult>();
            await RunStepAsync(step with { Name = $"{step.Name} (request {attempt}/{parallelRequests})", ParallelRequests = null }, new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase), cancellationToken, attemptResults);
            return attemptResults.Single();
        }).ToArray();
        try
        {
            results.AddRange(await Task.WhenAll(attempts));
        }
        catch
        {
            foreach (var attempt in attempts.Where(x => x.IsCompletedSuccessfully))
                results.Add(attempt.Result);
            throw;
        }
    }

    private string ResolveTemplate(string input, IReadOnlyDictionary<string, string> variables)
        => Templates.Resolve(input, variables, env);

    private string ResolveTemplate(string input, Dictionary<string, string> variables)
        => Templates.Resolve(input, variables, env);

    private string ResolveTemplate(
        string input,
        IReadOnlyDictionary<string, string> variables,
        IEnvironmentStore environment)
        => Templates.Resolve(input, variables, environment);

    private string ResolveTemplate(
        string input,
        Dictionary<string, string> variables,
        IEnvironmentStore environment)
        => Templates.Resolve(input, variables, environment);

    private static string ToUtcIso(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");

    private async Task RunStepWithRetryAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results)
    {
        if (step.Retry is null)
        {
            await RunStepAsync(step, variables, cancellationToken, results);
            return;
        }
        int timeoutMs = step.Retry.TimeoutMs ?? 10000;
        int intervalMs = step.Retry.IntervalMs ?? 500;
        var watch = Stopwatch.StartNew();
        Exception? lastException = null;
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            var attemptResults = new List<StepRunResult>();
            try
            {
                await RunStepAsync(step, variables, cancellationToken, attemptResults);
                results.AddRange(attemptResults);
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(intervalMs, cancellationToken);
            }
        }
        var finalResults = new List<StepRunResult>();
        try { await RunStepAsync(step, variables, cancellationToken, finalResults); }
        catch { results.AddRange(finalResults); throw lastException ?? new TimeoutException("Hết thời gian chờ kết quả."); }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (token is not null) return;
        var auth = project.Authentication ?? throw new InvalidOperationException("Chưa cấu hình xác thực.");
        string strategy = env.Get("AUTH_TYPE") ?? auth.Strategy;
        if (strategy.Equals("static-token", StringComparison.OrdinalIgnoreCase))
        {
            token = env.Require("AUTH_TOKEN");
            return;
        }
        if (!strategy.Equals("login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Không hỗ trợ kiểu xác thực: {strategy}");
        string loginPath = env.Get("AUTH_LOGIN_PATH") ?? auth.LoginPath ?? throw new InvalidOperationException("Thiếu đường dẫn đăng nhập. Cấu hình authentication.loginPath hoặc AUTH_LOGIN_PATH.");
        string method = env.Get("AUTH_LOGIN_METHOD") ?? auth.Method ?? "POST";
        using var content = new StringContent(ResolveTemplate(auth.Body?.GetRawText() ?? "{}", new Dictionary<string, string>(), env), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), ResolveTemplate(loginPath, new Dictionary<string, string>(), env)) { Content = content }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Đăng nhập tự động thất bại với HTTP {(int)response.StatusCode}. Kiểm tra AUTH_USERNAME/AUTH_PASSWORD và authentication.loginPath.");
        using var document = JsonDocument.Parse(body);
        string tokenPath = env.Get("AUTH_TOKEN_PATH") ?? auth.TokenPath ?? "$.accessToken";
        string[] tokenPaths = [tokenPath, "$.data.accessToken", "$.accessToken", "$.data.token", "$.token"];
        foreach (string candidate in tokenPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var selected = JsonPath.Select(document.RootElement, candidate);
            if (selected.Found && !string.IsNullOrWhiteSpace(JsonPath.Text(selected.Value))) { token = JsonPath.Text(selected.Value); return; }
        }
        throw new InvalidOperationException($"Không tìm thấy access token tại {string.Join(", ", tokenPaths.Distinct(StringComparer.OrdinalIgnoreCase))}. Cấu hình AUTH_TOKEN_PATH theo response đăng nhập.");
    }

    private void Guard(string url)
    {
        var uri = new Uri(url);
        var production = (project.Safety?.ProductionHosts ?? []).Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) ||
                         (env.Get("TEST_ENV") ?? "").Equals("production", StringComparison.OrdinalIgnoreCase);
        if (production && !env.Bool("ALLOW_PRODUCTION")) throw new InvalidOperationException("Môi trường production đang bị chặn để bảo vệ dữ liệu.");
    }

    private string Redact(string value)
    {
        foreach (var key in new[] { "AUTH_TOKEN", "AUTH_PASSWORD", "DB_CONNECTION_STRING", "OFFLINE_DB_CONNECTION_STRING", "ONLINE_DB_CONNECTION_STRING", "OFFLINE_CMS_AUTH_PASSWORD", "ONLINE_CMS_AUTH_PASSWORD", "TEST_DEVICE_PASSWORD", "REDIS_CONNECTION_STRING", "MQTT_PASSWORD", "MQTT_AUTH_DB_PASSWORD" })
            if (env.Get(key) is { Length: > 0 } secret) value = value.Replace(secret, "***", StringComparison.Ordinal);
        return value;
    }

    public void Dispose()
    {
        foreach (ITestStepExecutor executor in executorRegistry.All)
            executor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        http.Dispose();
    }
}
