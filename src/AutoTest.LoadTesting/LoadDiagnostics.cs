using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AutoTest.LoadTesting;

public static class LoadDiagnostics
{
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1)];
    }

    public static IReadOnlyDictionary<string, double> ParseServerTiming(IEnumerable<string> headerValues)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in string.Join(',', headerValues).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = item.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string? duration = parts.Skip(1).FirstOrDefault(part => part.StartsWith("dur=", StringComparison.OrdinalIgnoreCase));
            if (duration is not null && double.TryParse(duration[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds))
            {
                result[parts[0]] = milliseconds;
            }
        }

        return result;
    }

    public static string HashIdentifier(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12].ToLowerInvariant();

    public static string Category(int statusCode, string? exceptionType) => exceptionType switch
    {
        nameof(TaskCanceledException) => "timeout",
        not null => "connection-error",
        _ when statusCode == 423 => "lock-rejected",
        _ when statusCode is >= 200 and < 300 => "success",
        _ when statusCode >= 500 => "server-error",
        _ => "business-or-contract-error",
    };

    public static double BarrierSkewMs(IEnumerable<DateTimeOffset> startedAt)
    {
        DateTimeOffset[] values = startedAt.ToArray();
        return values.Length < 2 ? 0 : (values.Max() - values.Min()).TotalMilliseconds;
    }

    public static string FormatMilliseconds(double milliseconds)
        => $"{milliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms";
}
