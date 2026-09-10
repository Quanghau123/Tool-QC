using System.Globalization;
using System.Security;
using System.Text;
using AutoTest.Abstractions;

namespace AutoTest.Reporting.Html;

public static class JUnitReportWriter
{
    public static string Write(string directory, ProjectSpec project, IReadOnlyList<RunResult> results, DateTimeOffset startedAt)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{startedAt:yyyy-MM-dd_HHmmss}_{Sanitize(project.Name)}.junit.xml");
        int failures = results.Count(result => !result.Passed);
        string duration = results.Sum(result => result.Duration.TotalSeconds).ToString("F3", CultureInfo.InvariantCulture);
        StringBuilder xml = new();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine($"<testsuite name=\"{Escape(project.Name)}\" tests=\"{results.Count}\" failures=\"{failures}\" time=\"{duration}\" timestamp=\"{startedAt:O}\">");
        foreach (RunResult result in results)
        {
            string caseDuration = result.Duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture);
            xml.AppendLine($"  <testcase classname=\"{Escape(project.Name)}\" name=\"{Escape(result.Id)}\" time=\"{caseDuration}\">");
            if (!result.Passed)
            {
                string error = Escape(result.Error ?? "Test thất bại");
                xml.AppendLine($"    <failure message=\"{error}\">{error}</failure>");
            }
            xml.AppendLine("  </testcase>");
        }
        xml.AppendLine("</testsuite>");
        File.WriteAllText(path, xml.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
    private static string Sanitize(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}
