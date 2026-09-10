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

    public static string Classify(IEnumerable<string> lines)
    {
        string text = string.Join('\n', lines);
        if (text.Contains("Preflight", StringComparison.OrdinalIgnoreCase)) return "TEST_SCRIPT_ERROR";
        if (text.Contains("connection refused", StringComparison.OrdinalIgnoreCase) || text.Contains("No such host", StringComparison.OrdinalIgnoreCase) || text.Contains("timeout", StringComparison.OrdinalIgnoreCase)) return "ENVIRONMENT_ERROR";
        if (text.Contains("mong đợi", StringComparison.OrdinalIgnoreCase) || text.Contains("không đúng", StringComparison.OrdinalIgnoreCase)) return "BACKEND_OR_CONTRACT_FAILURE";
        return "RUNNER_ERROR";
    }

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
sealed record RunSnapshot(string Id, string Project, string[] CaseIds, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int? ExitCode, string? ReportPath, string FailureType, string[] Lines);
