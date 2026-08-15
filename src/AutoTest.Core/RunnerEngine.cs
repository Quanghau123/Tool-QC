using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Npgsql;
using System.Text.RegularExpressions;

namespace AutoTest.Core;

public sealed class RunnerEngine : IDisposable
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly ProjectSpec project;
    private readonly EnvironmentStore env;
    private readonly HttpClient http;
    private readonly MqttTestClient mqtt;
    private string? token;

    public RunnerEngine(ProjectSpec project, EnvironmentStore env)
    {
        this.project = project;
        this.env = env;
        mqtt = new MqttTestClient(env);
        var url = env.Require(project.BaseUrlVariable);
        Guard(url);
        http = new HttpClient
        {
            BaseAddress = new Uri(url),
            Timeout = TimeSpan.FromSeconds(int.TryParse(env.Get("API_TIMEOUT_SECONDS"), out var seconds) ? seconds : 30)
        };
        foreach (var header in project.DefaultHeaders ?? [])
            http.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    public async Task<RunResult> RunAsync(CaseSpec test, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var started = false;
        var stepResults = new List<StepRunResult>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unique"] = Guid.NewGuid().ToString("N"),
            ["timestamp"] = now.ToUnixTimeSeconds().ToString(),
            ["timestampMs"] = now.ToUnixTimeMilliseconds().ToString(),
            ["nowIso"] = ToUtcIso(now),
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

        try
        {
            if (test.Destructive && !env.Bool("ALLOW_DESTRUCTIVE_TESTS"))
                throw new InvalidOperationException("Kịch bản có thay đổi dữ liệu đang bị chặn. Hãy bật ALLOW_DESTRUCTIVE_TESTS trên môi trường kiểm thử an toàn.");
            foreach (var variable in test.Variables ?? [])
                variables[variable.Key] = Templates.Resolve(variable.Value, variables, env);
            started = true;
            foreach (var step in test.Steps)
                await RunConfiguredStepAsync(step, variables, cancellationToken, stepResults);
            return new(test.Id, test.Name, true, watch.Elapsed, null, stepResults);
        }
        catch (Exception exception)
        {
            return new(test.Id, test.Name, false, watch.Elapsed, Redact(exception.Message), stepResults);
        }
        finally
        {
            if (started)
                foreach (var step in test.Cleanup ?? [])
                    try
                    {
                        if (!CanResolveCleanupStep(step, variables))
                        {
                            Console.Error.WriteLine($"Bỏ qua bước dọn dữ liệu '{step.Name}' vì dữ liệu cần thiết chưa được tạo.");
                            continue;
                        }
                        await RunStepAsync(step, variables, cancellationToken, stepResults, false, true);
                    }
                    catch (Exception exception) { Console.Error.WriteLine($"Dọn dữ liệu thất bại cho kịch bản {test.Id}: {Redact(exception.Message)}"); }
        }
    }

    private async Task RunStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results, bool assertions = true, bool cleanup = false)
    {
        if (step.Request.Database is not null)
        {
            await RunDatabaseStepAsync(step, variables, cancellationToken, results, assertions, cleanup);
            return;
        }
        if (step.Request.Mqtt is not null)
        {
            await RunMqttStepAsync(step, variables, cancellationToken, results, assertions, cleanup);
            return;
        }
        var watch = Stopwatch.StartNew();
        var method = step.Request.Method ?? throw new InvalidDataException($"Thiếu phương thức HTTP cho bước: {step.Name}");
        var path = Templates.Resolve(step.Request.Path ?? throw new InvalidDataException($"Thiếu đường dẫn HTTP cho bước: {step.Name}"), variables, env);
        var payload = step.Request.Body is { } body ? Templates.Resolve(body.GetRawText(), variables, env) : null;
        var form = step.Request.Form?.ToDictionary(
            item => item.Key,
            item => Templates.Resolve(item.Value, variables, env));
        var reportPayload = payload ?? (form is null ? null : JsonSerializer.Serialize(form));
        var expectedText = assertions ? DescribeExpected(step.Expect, variables) : "Bước dọn dữ liệu: không đối chiếu kết quả";
        int? actualStatus = null;
        string? responseText = null;
        try
        {
            var requiresAuth = !string.IsNullOrWhiteSpace(step.Auth);
            string? requestToken = null;
            if (!string.IsNullOrWhiteSpace(step.AuthToken))
                requestToken = Templates.Resolve(step.AuthToken, variables, env);
            else if (requiresAuth)
            {
                await AuthenticateAsync(cancellationToken);
                requestToken = token;
            }
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (requiresAuth && requestToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue(project.Authentication?.Prefix ?? "Bearer", requestToken);
            foreach (var header in step.Request.Headers ?? [])
                request.Headers.TryAddWithoutValidation(header.Key, Templates.Resolve(header.Value, variables, env));
            if (payload is not null && form is not null)
                throw new InvalidDataException($"Bước '{step.Name}' không thể gửi đồng thời body JSON và form.");
            if (payload is not null)
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            else if (form is not null)
            {
                var formContent = new MultipartFormDataContent();
                foreach (var item in form)
                    formContent.Add(new StringContent(item.Value), item.Key);
                request.Content = formContent;
            }

            using var response = await http.SendAsync(request, cancellationToken);
            actualStatus = (int)response.StatusCode;
            responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (cleanup && !response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Bước dọn dữ liệu nhận mã HTTP {actualStatus}. Nội dung phản hồi: {Redact(responseText)}");
            if (assertions)
            {
                var expected = step.Expect ?? throw new InvalidDataException($"Thiếu cấu hình kết quả mong đợi cho bước: {step.Name}");
                if (expected.Status is not { } expectedStatus)
                    throw new InvalidDataException($"Thiếu mã trạng thái HTTP mong đợi cho bước: {step.Name}");
                if (actualStatus != expectedStatus)
                    throw new InvalidOperationException($"{step.Name}: mong đợi mã HTTP {expected.Status}, thực tế nhận {actualStatus}. Nội dung phản hồi: {Redact(responseText)}");
                if (expected.MaxResponseTimeMs is { } max && watch.ElapsedMilliseconds > max)
                    throw new InvalidOperationException($"Thời gian phản hồi vượt quá giới hạn {max} ms.");
                AssertAndSave(step, expected, responseText, variables);
            }
            results.Add(new(step.Name, cleanup, true, method, path, SanitizeJson(reportPayload), expectedText, actualStatus, SanitizeJson(responseText), watch.Elapsed, null));
        }
        catch (Exception exception)
        {
            var error = Redact(exception.Message);
            results.Add(new(step.Name, cleanup, false, method, path, SanitizeJson(reportPayload), expectedText, actualStatus, SanitizeJson(responseText), watch.Elapsed, error));
            throw;
        }
    }

    private void AssertAndSave(StepSpec step, ExpectSpec expected, string responseText, Dictionary<string, string> variables)
    {
        if ((expected.Json?.Count ?? 0) == 0 && step.Save is null) return;
        using var document = JsonDocument.Parse(responseText);
        foreach (var assertion in expected.Json ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, assertion.Key);
            if (assertion.Value.Exists is { } exists && selected.Found != exists) throw new InvalidOperationException($"Trường {assertion.Key} không đúng yêu cầu về sự tồn tại.");
            if (!selected.Found) continue;
            var actual = JsonPath.Text(selected.Value);
            if (assertion.Value.ExpectedValue is { } value && actual != Templates.Resolve(JsonPath.Text(value), variables, env)) throw new InvalidOperationException($"Giá trị của trường {assertion.Key} không đúng như mong đợi.");
            if (assertion.Value.Contains is { } contains && !actual.Contains(Templates.Resolve(contains, variables, env), StringComparison.Ordinal)) throw new InvalidOperationException($"Trường {assertion.Key} không chứa nội dung mong đợi.");
        }
        foreach (var saved in step.Save ?? [])
        {
            var selected = JsonPath.Select(document.RootElement, saved.Value);
            if (!selected.Found) throw new InvalidOperationException($"Không thể lưu biến {saved.Key} từ phản hồi.");
            variables[saved.Key] = JsonPath.Text(selected.Value);
        }
    }

    private async Task RunDatabaseStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results, bool assertions, bool cleanup)
    {
        var watch = Stopwatch.StartNew();
        string commandText = step.Request.Database!.Command;
        string expectedText = assertions ? "Lệnh PostgreSQL thực thi thành công" : "Bước dọn dữ liệu PostgreSQL";
        try
        {
            var resolvedParameters = (step.Request.Database.Parameters ?? [])
                .ToDictionary(x => x.Key, x => Templates.Resolve(x.Value, variables, env), StringComparer.OrdinalIgnoreCase);
            var parameterOrder = new List<string>();
            commandText = Regex.Replace(commandText, @"@([A-Za-z_][A-Za-z0-9_]*)", match =>
            {
                string name = match.Groups[1].Value;
                if (!resolvedParameters.ContainsKey(name))
                    throw new InvalidDataException($"Thiếu tham số PostgreSQL: {name}");
                int index = parameterOrder.FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    parameterOrder.Add(name);
                    index = parameterOrder.Count - 1;
                }
                return $"${index + 1}";
            });
            await using var connection = new NpgsqlConnection(env.Require("DB_CONNECTION_STRING"));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(commandText, connection);
            foreach (string parameterName in parameterOrder)
                command.Parameters.Add(new NpgsqlParameter { Value = resolvedParameters[parameterName] });
            int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            results.Add(new(step.Name, cleanup, true, "POSTGRESQL", "database", null, expectedText, null, $"Số dòng ảnh hưởng: {affectedRows}", watch.Elapsed, null));
        }
        catch (Exception exception)
        {
            string error = Redact(exception.Message);
            results.Add(new(step.Name, cleanup, false, "POSTGRESQL", "database", null, expectedText, null, null, watch.Elapsed, error));
            throw;
        }
    }

    private async Task RunConfiguredStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results)
    {
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

    private async Task RunMqttStepAsync(StepSpec step, Dictionary<string, string> variables, CancellationToken cancellationToken, List<StepRunResult> results, bool assertions, bool cleanup)
    {
        var watch = Stopwatch.StartNew();
        MqttRequestSpec request = step.Request.Mqtt!;
        string action = request.Action.ToLowerInvariant();
        string topic = ResolveMqttTopic(request.Topic, variables);
        string payload = Templates.Resolve(request.Payload ?? string.Empty, variables, env);
        int qos = request.Qos ?? 1;
        bool retain = request.Retain ?? false;
        int timeoutMs = request.TimeoutMs ?? env.Int("MQTT_TIMEOUT_MS", 10000);
        string? username = ResolveOptional(request.Username, variables);
        string? password = ResolveOptional(request.Password, variables);
        string? clientId = ResolveOptional(request.ClientId, variables);
        string expectedText = assertions ? DescribeMqttExpected(step.Expect, variables) : "Bước dọn dữ liệu MQTT: không đối chiếu kết quả";
        string? actual = null;
        try
        {
            if (timeoutMs <= 0) throw new InvalidOperationException("MQTT timeoutMs phải lớn hơn 0.");
            if (qos is < 0 or > 2) throw new InvalidOperationException("MQTT QoS chỉ hỗ trợ các giá trị 0, 1 hoặc 2.");
            MqttReceivedMessage? received;
            switch (action)
            {
                case "connect":
                    await mqtt.ConnectAsync(username, password, clientId, cancellationToken);
                    received = null;
                    break;
                case "publish":
                    await mqtt.PublishAsync(topic, payload, qos, retain, username, password, clientId, cancellationToken);
                    received = null;
                    break;
                case "subscribe":
                    received = await mqtt.SubscribeAsync(topic, qos, TimeSpan.FromMilliseconds(timeoutMs), username, password, clientId, cancellationToken);
                    break;
                case "roundtrip":
                    received = await mqtt.RoundtripAsync(topic, payload, qos, retain, TimeSpan.FromMilliseconds(timeoutMs), username, password, clientId, cancellationToken);
                    break;
                case "lastwill":
                    if (request.Will is null) throw new InvalidOperationException("Thao tác MQTT lastwill yêu cầu object will.");
                    topic = ResolveMqttTopic(request.Will.Topic, variables);
                    if (topic.Length == 0) throw new InvalidOperationException("MQTT will.topic không được để trống.");
                    qos = request.Will.Qos ?? 1;
                    if (qos is < 0 or > 2) throw new InvalidOperationException("MQTT will.qos chỉ hỗ trợ các giá trị 0, 1 hoặc 2.");
                    payload = Templates.Resolve(request.Will.Payload ?? string.Empty, variables, env);
                    retain = request.Will.Retain ?? false;
                    received = await mqtt.LastWillAsync(
                        topic,
                        payload,
                        qos,
                        retain,
                        TimeSpan.FromMilliseconds(timeoutMs),
                        username,
                        password,
                        clientId,
                        (stageName, stageActual) => results.Add(new(
                            stageName,
                            cleanup,
                            true,
                            "MQTT LASTWILL",
                            topic,
                            null,
                            "Giai đoạn phải hoàn thành thành công",
                            null,
                            stageActual,
                            watch.Elapsed,
                            null)),
                        cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Không hỗ trợ thao tác MQTT: {request.Action}");
            }
            actual = received is null ? "Đã thực hiện thành công." : JsonSerializer.Serialize(new { received.Topic, received.Payload }, ReportJsonOptions);
            if (assertions && received is not null) AssertMqtt(step.Expect?.Mqtt, received, variables);
            results.Add(new(step.Name, cleanup, true, $"MQTT {action.ToUpperInvariant()}", topic, payload.Length == 0 ? null : Redact(payload), expectedText, null, actual, watch.Elapsed, null));
        }
        catch (Exception exception)
        {
            string error = Redact(exception.Message);
            results.Add(new(step.Name, cleanup, false, $"MQTT {action.ToUpperInvariant()}", topic, payload.Length == 0 ? null : Redact(payload), expectedText, null, actual, watch.Elapsed, error));
            throw;
        }
    }

    private string? ResolveOptional(string? value, IReadOnlyDictionary<string, string> variables)
        => string.IsNullOrWhiteSpace(value) ? null : Templates.Resolve(value, variables, env);

    private string ResolveMqttTopic(string? topic, IReadOnlyDictionary<string, string> variables)
    {
        string resolved = Templates.Resolve(topic ?? string.Empty, variables, env).Trim('/');
        if (resolved.Length == 0) return string.Empty;
        string prefix = (env.Get("MQTT_PREFIX") ?? string.Empty).Trim('/');
        return prefix.Length == 0 || resolved.StartsWith(prefix + "/", StringComparison.Ordinal) || resolved == prefix
            ? resolved
            : $"{prefix}/{resolved}";
    }

    private void AssertMqtt(MqttExpectSpec? expected, MqttReceivedMessage received, IReadOnlyDictionary<string, string> variables)
    {
        if (expected is null) return;
        if (expected.Topic is { } topic && received.Topic != ResolveMqttTopic(topic, variables))
            throw new InvalidOperationException("Topic MQTT nhận được không đúng như mong đợi.");
        if (expected.Payload is { } payload && received.Payload != Templates.Resolve(payload, variables, env))
            throw new InvalidOperationException("Nội dung MQTT nhận được không đúng như mong đợi.");
        if (expected.PayloadContains is { } contains && !received.Payload.Contains(Templates.Resolve(contains, variables, env), StringComparison.Ordinal))
            throw new InvalidOperationException("Nội dung MQTT không chứa giá trị mong đợi.");
    }

    private string DescribeMqttExpected(ExpectSpec? expected, IReadOnlyDictionary<string, string> variables)
    {
        if (expected?.Mqtt is null) return "Thao tác MQTT hoàn thành thành công";
        var lines = new List<string>();
        if (expected.Mqtt.Topic is { } topic) lines.Add($"Topic nhận được = {ResolveMqttTopic(topic, variables)}");
        if (expected.Mqtt.Payload is { } payload) lines.Add($"Nội dung nhận được = {Templates.Resolve(payload, variables, env)}");
        if (expected.Mqtt.PayloadContains is { } contains) lines.Add($"Nội dung nhận được có chứa {Templates.Resolve(contains, variables, env)}");
        return string.Join("\n", lines);
    }

    private string DescribeExpected(ExpectSpec? expected, Dictionary<string, string> variables)
    {
        if (expected is null) return "Thiếu cấu hình kết quả mong đợi";
        var details = new List<string>();
        if (expected.Status is { } status) details.Add($"Mã trạng thái HTTP = {status}");
        if (expected.MaxResponseTimeMs is { } max) details.Add($"Thời gian phản hồi không quá {max} ms");
        foreach (var item in expected.Json ?? [])
        {
            var rules = new List<string>();
            if (item.Value.Exists is { } exists) rules.Add(exists ? "tồn tại" : "không tồn tại");
            if (item.Value.ExpectedValue is { } value) rules.Add($"bằng {Templates.Resolve(JsonPath.Text(value), variables, env)}");
            if (item.Value.Contains is { } contains) rules.Add($"chứa {Templates.Resolve(contains, variables, env)}");
            details.Add($"{item.Key}: {string.Join(", ", rules)}");
        }
        return string.Join("\n", details);
    }

    private string? SanitizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(SanitizeElement(document.RootElement), ReportJsonOptions);
        }
        catch { return Redact(value); }
    }

    private bool CanResolveCleanupStep(StepSpec step, IReadOnlyDictionary<string, string> variables)
    {
        try
        {
            if (step.Request.Path is { } path) Templates.Resolve(path, variables, env);
            if (step.AuthToken is { } authToken) Templates.Resolve(authToken, variables, env);
            if (step.Request.Mqtt is { } mqttRequest)
            {
                if (mqttRequest.Topic is { } topic) Templates.Resolve(topic, variables, env);
                if (mqttRequest.Payload is { } mqttPayload) Templates.Resolve(mqttPayload, variables, env);
                if (mqttRequest.Username is { } username) Templates.Resolve(username, variables, env);
                if (mqttRequest.Password is { } password) Templates.Resolve(password, variables, env);
                if (mqttRequest.ClientId is { } clientId) Templates.Resolve(clientId, variables, env);
                if (mqttRequest.Will?.Topic is { } willTopic) Templates.Resolve(willTopic, variables, env);
                if (mqttRequest.Will?.Payload is { } willPayload) Templates.Resolve(willPayload, variables, env);
            }
            if (step.Request.Database is { } databaseRequest)
                foreach (var parameter in databaseRequest.Parameters ?? []) Templates.Resolve(parameter.Value, variables, env);
            if (step.Request.Body is { } body)
                Templates.Resolve(body.GetRawText(), variables, env);
            foreach (var formItem in step.Request.Form ?? [])
                Templates.Resolve(formItem.Value, variables, env);
            foreach (var header in step.Request.Headers ?? [])
                Templates.Resolve(header.Value, variables, env);
            return true;
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("Không tìm thấy biến:", StringComparison.Ordinal))
        {
            return false;
        }
    }

    private object? SanitizeElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => IsSensitive(property.Name) ? (object?)"***" : SanitizeElement(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(SanitizeElement).ToArray(),
        JsonValueKind.String => Redact(element.GetString() ?? string.Empty),
        JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static bool IsSensitive(string name) => new[] { "password", "confirmPassword", "token", "accessToken", "refreshToken", "authorization", "secret", "connectionString" }
        .Any(key => name.Contains(key, StringComparison.OrdinalIgnoreCase));

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (token is not null) return;
        var auth = project.Authentication ?? throw new InvalidOperationException("Chưa cấu hình xác thực.");
        if (auth.Strategy.Equals("static-token", StringComparison.OrdinalIgnoreCase))
        {
            token = env.Require("AUTH_TOKEN");
            return;
        }
        if (!auth.Strategy.Equals("login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Không hỗ trợ kiểu xác thực: {auth.Strategy}");
        using var content = new StringContent(Templates.Resolve(auth.Body?.GetRawText() ?? "{}", new Dictionary<string, string>(), env), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(new HttpRequestMessage(new HttpMethod(auth.Method ?? "POST"), auth.LoginPath) { Content = content }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(body);
        var selected = JsonPath.Select(document.RootElement, auth.TokenPath ?? "$.accessToken");
        token = selected.Found ? JsonPath.Text(selected.Value) : throw new InvalidOperationException("Không tìm thấy access token trong phản hồi đăng nhập.");
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
        foreach (var key in new[] { "AUTH_TOKEN", "AUTH_PASSWORD", "DB_CONNECTION_STRING", "REDIS_CONNECTION_STRING", "MQTT_PASSWORD", "MQTT_AUTH_DB_PASSWORD" })
            if (env.Get(key) is { Length: > 0 } secret) value = value.Replace(secret, "***", StringComparison.Ordinal);
        return value;
    }

    public void Dispose()
    {
        mqtt.DisposeAsync().AsTask().GetAwaiter().GetResult();
        http.Dispose();
    }
}
