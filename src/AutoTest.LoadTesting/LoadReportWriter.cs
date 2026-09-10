using System.Text;
using System.Text.Json;

namespace AutoTest.LoadTesting;

// Sinh báo cáo chẩn đoán hiệu năng từ các timing record đã được làm sạch.
public static class LoadReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<LoadTiming> ReadJsonl(string path)
        => File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => ReadLine(line, path))
            .ToArray();

    private static LoadTiming ReadLine(string line, string path)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        if (document.RootElement.TryGetProperty("batchId", out _))
            return JsonSerializer.Deserialize<LoadTiming>(line, JsonOptions) ?? throw new InvalidDataException($"JSONL không hợp lệ: {path}");

        LegacyLoadTiming legacy = JsonSerializer.Deserialize<LegacyLoadTiming>(line, JsonOptions)
            ?? throw new InvalidDataException($"JSONL cũ không hợp lệ: {path}");
        return new(legacy.RecordedAtUtc, legacy.RecordedAtLocal, legacy.RecordedAtUtc, legacy.RecordedAtUtc,
            legacy.ScenarioRunId, "legacy", legacy.WorkerId, legacy.Iteration, legacy.Mode,
            LoadDiagnostics.HashIdentifier(legacy.EventId), LoadDiagnostics.HashIdentifier(legacy.CustomerIdMasked),
            LoadDiagnostics.HashIdentifier(legacy.CartIdMasked), LoadDiagnostics.HashIdentifier(legacy.GiftId), legacy.Endpoint,
            legacy.Method, legacy.NormalizedPath, legacy.DurationMs, legacy.StatusCode, legacy.Success, legacy.Attempt,
            LoadDiagnostics.Category(legacy.StatusCode, null), legacy.ErrorCode, legacy.CorrelationId, null,
            new Dictionary<string, double>(), null);
    }

    public static string Create(IReadOnlyCollection<LoadTiming> rows, string title, int slowThresholdMs)
    {
        var output = new StringBuilder()
            .AppendLine($"# Load diagnostic report — {title}")
            .AppendLine()
            .AppendLine($"- Records: `{rows.Count}`; slow threshold: `{slowThresholdMs:N0} ms`.")
            .AppendLine("- Client duration includes client queue, network and server time; it does not prove lock hold time.")
            .AppendLine("- Server timing is evidence only when the backend returns `Server-Timing`.")
            .AppendLine()
            .AppendLine("## Endpoint summary")
            .AppendLine("| Endpoint | Total | Success | Failure | 423 | Timeout | Slow % | Avg ms | P50 ms | P95 ms | P99 ms | Max ms |")
            .AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (IGrouping<string, LoadTiming> group in rows.GroupBy(row => row.Endpoint).OrderBy(group => group.Key))
        {
            double[] durations = group.Select(row => row.DurationMs).ToArray();
            output.AppendLine($"| {group.Key} | {group.Count()} | {group.Count(row => row.Success)} | {group.Count(row => !row.Success)} | {group.Count(row => row.StatusCode == 423)} | {group.Count(row => row.Category == "timeout")} | {100d * group.Count(row => row.DurationMs > slowThresholdMs) / group.Count():F2}% | {durations.Average():F0} | {LoadDiagnostics.Percentile(durations, .5):F0} | {LoadDiagnostics.Percentile(durations, .95):F0} | {LoadDiagnostics.Percentile(durations, .99):F0} | {durations.Max():F0} |");
        }

        AppendServerTiming(output, rows);
        AppendFailures(output, rows);
        output.AppendLine().AppendLine("## Root-cause matrix")
            .AppendLine("| Observation | Evidence available | Current conclusion |")
            .AppendLine("|---|---|---|")
            .AppendLine("| Response gần lock timeout hoặc HTTP 423 | Client duration/status | Có contention, chưa biết lock hold nếu thiếu server timing. |")
            .AppendLine("| Tổng response cao nhưng lock timing thấp | `Server-Timing` | Ưu tiên query sau lock, network hoặc client queue. |")
            .AppendLine("| Confirm lỗi nhưng commit=true | Post-failure verification | Ưu tiên idempotency và response sequencing. |")
            .AppendLine("| Put-only tốt, cross-operation xấu | So sánh mode | Hai operation có khả năng chặn nhau trên shared resource lock. |")
            .AppendLine()
            .AppendLine("## Units")
            .AppendLine("- Mọi duration là milliseconds. `2,323 ms` bằng `2.323 giây`, không phải `2.323 ms`.");
        return output.ToString();
    }

    public static string Compare(IReadOnlyDictionary<string, IReadOnlyCollection<LoadTiming>> runs, int slowThresholdMs)
    {
        var output = new StringBuilder("# Multi-run load comparison\n\n")
            .AppendLine("| Run | Endpoint | Endpoint | Requests | Failure | 423 | Slow % | Avg ms | P95 ms | P99 ms |")
            .AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach ((string run, IReadOnlyCollection<LoadTiming> rows) in runs)
        foreach (IGrouping<string, LoadTiming> group in rows.GroupBy(row => row.Endpoint).OrderBy(group => group.Key))
        {
            double[] values = group.Select(row => row.DurationMs).ToArray();
            string mode = group.First().Mode;
            output.AppendLine($"| {run} | {mode} | {group.Key} | {group.Count()} | {group.Count(row => !row.Success)} | {group.Count(row => row.StatusCode == 423)} | {100d * group.Count(row => row.DurationMs > slowThresholdMs) / group.Count():F2}% | {values.Average():F0} | {LoadDiagnostics.Percentile(values, .95):F0} | {LoadDiagnostics.Percentile(values, .99):F0} |");
        }
        return output.ToString();
    }

    private static void AppendServerTiming(StringBuilder output, IReadOnlyCollection<LoadTiming> rows)
    {
        output.AppendLine().AppendLine("## Server timing");
        var timings = rows.SelectMany(row => row.ServerTimingMs.Select(item => new { row.Endpoint, item.Key, item.Value })).ToArray();
        if (timings.Length == 0)
        {
            output.AppendLine("- Backend chưa trả `Server-Timing`; chưa thể phân biệt lock wait, lock hold, transaction và DB time.");
            return;
        }
        output.AppendLine("| Endpoint | Metric | Samples | Avg ms | P95 ms | Max ms |").AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var group in timings.GroupBy(item => new { item.Endpoint, item.Key }).OrderBy(group => group.Key.Endpoint).ThenBy(group => group.Key.Key))
        {
            double[] values = group.Select(item => item.Value).ToArray();
            output.AppendLine($"| {group.Key.Endpoint} | {group.Key.Key} | {values.Length} | {values.Average():F1} | {LoadDiagnostics.Percentile(values, .95):F1} | {values.Max():F1} |");
        }
    }

    private static void AppendFailures(StringBuilder output, IReadOnlyCollection<LoadTiming> rows)
    {
        output.AppendLine().AppendLine("## Failure and commit outcome");
        LoadTiming[] failures = rows.Where(row => !row.Success).ToArray();
        if (failures.Length == 0) { output.AppendLine("- Không có request thất bại."); return; }
        foreach (var group in failures.GroupBy(row => new { row.Endpoint, row.Category, row.StatusCode, row.CommitCompleted }))
            output.AppendLine($"- `{group.Key.Endpoint}` `{group.Key.Category}` HTTP `{group.Key.StatusCode}` commit `{group.Key.CommitCompleted?.ToString() ?? "unknown"}`: {group.Count()}.");
    }
}
