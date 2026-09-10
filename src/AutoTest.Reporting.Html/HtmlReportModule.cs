using AutoTest.Abstractions;
using AutoTest.Core;
namespace AutoTest.Reporting.Html;

// Nhóm báo cáo HTML: lỗi trình bày kết quả testcase hoặc đường dẫn report kiểm tra tại đây.
public sealed class HtmlReportModule : IReportWriter
{
    public string Write(string directory, ProjectSpec project, string environment, IReadOnlyList<RunResult> results, DateTimeOffset startedAt, IReadOnlyCollection<string>? selectedTags = null)
        => HtmlReportWriter.Write(directory, project, environment, results, startedAt, selectedTags);
}
