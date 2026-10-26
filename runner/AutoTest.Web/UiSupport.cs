using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

// Tiện ích backend UI: kiểm tra kết nối, lịch sử chạy và quản lý artifact an toàn.
static class UiSupport
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<ConnectionCheck[]> CheckConnectionsAsync(string root, string projectDirectory)
    {
        Dictionary<string, string> values = ReadEnvironment(Path.Combine(root, ".env"));
        foreach ((string key, string value) in ReadEnvironment(Path.Combine(projectDirectory, ".env"))) values[key] = value;
        foreach (string key in values.Keys.ToArray()) if (Environment.GetEnvironmentVariable(key) is { } systemValue) values[key] = systemValue;

        List<ConnectionCheck> checks = [];
        foreach ((string key, string value) in values.Where(item => item.Key.EndsWith("_URL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Value)))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) { checks.Add(new(key, false, "URL không hợp lệ", 0)); continue; }
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
                using HttpRequestMessage request = new(HttpMethod.Head, uri);
                using HttpResponseMessage response = await client.SendAsync(request);
                checks.Add(new(key, true, $"HTTP {(int)response.StatusCode}", watch.ElapsedMilliseconds));
            }
            catch (Exception exception) { checks.Add(new(key, false, ShortMessage(exception), watch.ElapsedMilliseconds)); }
        }
        foreach ((string key, string value) in values.Where(item => item.Key.Contains("CONNECTION_STRING", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Value)))
        {
            Dictionary<string, string> parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Split('=', 2)).Where(part => part.Length == 2).ToDictionary(part => part[0].Trim(), part => part[1].Trim(), StringComparer.OrdinalIgnoreCase);
            string? host = parts.GetValueOrDefault("Host") ?? parts.GetValueOrDefault("Server");
            int port = int.TryParse(parts.GetValueOrDefault("Port"), out int configuredPort) ? configuredPort : key.Contains("REDIS", StringComparison.OrdinalIgnoreCase) ? 6379 : 5432;
            if (!string.IsNullOrWhiteSpace(host)) checks.Add(await CheckTcpAsync(key, host, port));
        }
        if (values.GetValueOrDefault("MQTT_HOST") is { Length: > 0 } mqttHost) checks.Add(await CheckTcpAsync("MQTT_HOST", mqttHost, int.TryParse(values.GetValueOrDefault("MQTT_PORT"), out int mqttPort) ? mqttPort : 1883));
        return checks.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).OrderBy(item => item.Key).ToArray();
    }

    private static async Task<ConnectionCheck> CheckTcpAsync(string key, string host, int port)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try { using System.Net.Sockets.TcpClient client = new(); await client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5)); return new(key, true, $"TCP {host}:{port}", watch.ElapsedMilliseconds); }
        catch (Exception exception) { return new(key, false, ShortMessage(exception), watch.ElapsedMilliseconds); }
    }

    public static object[] ReadHistory(string directory) => Directory.EnumerateFiles(directory, "*.json")
        .OrderByDescending(File.GetLastWriteTimeUtc).Take(100)
        .Select(path => JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(path), Options))
        .Where(item => item is not null).Cast<object>().ToArray();

    public static RunSnapshot? ReadHistory(string directory, string id)
    {
        string path = HistoryPath(directory, id);
        return File.Exists(path) ? JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(path), Options) : null;
    }

    public static void SaveHistory(string directory, RunSnapshot snapshot)
        => File.WriteAllText(HistoryPath(directory, snapshot.Id), JsonSerializer.Serialize(snapshot, Options));

    private static readonly IReadOnlyDictionary<string, DiagnosticCatalogueEntry> DiagnosticCatalogue =
        new Dictionary<string, DiagnosticCatalogueEntry>(StringComparer.Ordinal)
        {
            ["TQC-ENV-001"] = new("ENVIRONMENT", "Required environment configuration is missing.", "Open Configuration and provide the missing variable reported in the execution log."),
            ["TQC-ENV-002"] = new("ENVIRONMENT", "The configured host name cannot be resolved.", "Verify the host name, DNS, VPN, and network routing in Configuration."),
            ["TQC-ENV-003"] = new("ENVIRONMENT", "The target service refused the connection.", "Verify that the service is running and that the configured host and port are correct."),
            ["TQC-ENV-004"] = new("ENVIRONMENT", "The operation exceeded the configured timeout.", "Check service health and network latency; increase the timeout only when the expected workload requires it."),
            ["TQC-AUTH-001"] = new("AUTHENTICATION", "Authentication failed or the credential is no longer valid.", "Refresh the token or credentials and verify the configured authentication strategy."),
            ["TQC-TST-001"] = new("TESTCASE", "The testcase failed preflight validation.", "Check testcase JSON, coverage, variables, and executor mapping in TestcaseValidator.cs and Execution.cs."),
            ["TQC-API-001"] = new("BACKEND_CONTRACT", "The backend result does not match the expected contract.", "Open the report and compare the failed step with the API response, database state, and backend contract."),
            ["TQC-RUN-001"] = new("RUNNER", "The Tool-QC runtime encountered an internal execution error.", "Follow the first Tool-QC stack frame and fix the referenced module under src/AutoTest.* or runner/AutoTest.*."),
            ["TQC-RUN-999"] = new("RUNNER", "The execution failed with an unclassified Tool-QC error.", "Inspect the first error and stack trace; start at RunnerEngine.cs, then follow the referenced executor or runner module.")
        };

    // Phân loại lỗi để UI chỉ đúng nhóm code hoặc cấu hình cần kiểm tra trước.
    public static FailureDiagnosis Diagnose(IEnumerable<string> lines)
    {
        string text = string.Join('\n', lines);
        string code = ContainsAny(text, "Thiếu cấu hình bắt buộc") ? "TQC-ENV-001"
            : ContainsAny(text, "No such host", "Name or service not known") ? "TQC-ENV-002"
            : ContainsAny(text, "connection refused", "actively refused") ? "TQC-ENV-003"
            : ContainsAny(text, "timeout", "timed out", "HttpClient.Timeout") ? "TQC-ENV-004"
            : ContainsAny(text, "authentication failed", "Unauthorized", "HTTP 401") ? "TQC-AUTH-001"
            : ContainsAny(text, "Preflight", "Thiếu request", "Không có executor", "Có nhiều executor", "Không tìm thấy biến") ? "TQC-TST-001"
            : ContainsAny(text, "mong đợi", "không đúng", "không thuộc tập", "không chứa", "HTTP 4", "HTTP 5") ? "TQC-API-001"
            : ContainsAny(text, "Build FAILED", "error CS", "Unhandled exception", "ObjectDisposedException", "NullReferenceException") ? "TQC-RUN-001"
            : "TQC-RUN-999";
        DiagnosticCatalogueEntry entry = DiagnosticCatalogue[code];
        return new(code, entry.Category, entry.Message, entry.FixHint);
    }

    public static string Classify(IEnumerable<string> lines) => Diagnose(lines).Code;

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    public static void SaveMessageScan(string root, string scanId, string reportPath)
    {
        string directory = Path.Combine(root, ".artifacts", "message-scans", ".ui");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, scanId + ".txt"), Path.GetFullPath(reportPath));
    }

    public static string? ReadMessageScan(string root, string scanId)
    {
        if (!Regex.IsMatch(scanId, "^[a-zA-Z0-9]+$")) return null;
        string path = Path.Combine(root, ".artifacts", "message-scans", ".ui", scanId + ".txt");
        if (!File.Exists(path)) return null;
        string report = Path.GetFullPath(File.ReadAllText(path).Trim());
        string allowedRoot = Path.GetFullPath(Path.Combine(root, ".artifacts", "message-scans"));
        return report.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? report : null;
    }

    private static Dictionary<string, string> ReadEnvironment(string path)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return values;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim(); int separator = line.IndexOf('=');
            if (separator > 0 && !line.StartsWith('#')) values[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
        }
        return values;
    }

    private static string HistoryPath(string directory, string id) => Path.Combine(directory, Regex.Replace(id, "[^a-zA-Z0-9]", "") + ".json");
    private static string ShortMessage(Exception exception) { string message = exception.GetBaseException().Message; return message.Length > 180 ? message[..180] : message; }
}

sealed record ConnectionCheck(string Key, bool Success, string Message, long DurationMs);
sealed record DiagnosticCatalogueEntry(string Category, string Message, string FixHint);
sealed record FailureDiagnosis(string Code, string Category, string Message, string FixHint);
sealed record RunSnapshot(string Id, string Project, string[] CaseIds, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int? ExitCode, string? ReportPath, string FailureType, string FailureCategory, string FailureMessage, string FixHint, string[] Lines);


