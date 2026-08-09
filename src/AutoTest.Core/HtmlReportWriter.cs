using System.Net;
using System.Text;

namespace AutoTest.Core;

public static class HtmlReportWriter
{
    public static string Write(string directory, ProjectSpec project, string environment, IReadOnlyList<RunResult> results, DateTimeOffset startedAt)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{project.Name}-{startedAt:yyyyMMdd-HHmmss}.html");
        var passed = results.Count(result => result.Passed);
        var builder = new StringBuilder();
        builder.Append("""
<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Báo cáo kiểm thử API</title><style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f5f7fa;color:#172033;margin:0;padding:28px}main{max-width:1200px;margin:auto}.card,details{background:white;border:1px solid #dce2ea;border-radius:10px;margin:14px 0;padding:18px}.summary{display:flex;gap:20px;flex-wrap:wrap}.metric{font-size:24px;font-weight:700}.pass{color:#087f5b}.fail{color:#c92a2a}.muted{color:#667085}summary{cursor:pointer;font-weight:650}pre{white-space:pre-wrap;word-break:break-word;background:#f8fafc;border:1px solid #e4e7ec;border-radius:6px;padding:12px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.badge{display:inline-block;padding:3px 9px;border-radius:999px;background:#eef2f6;margin-right:7px}@media(max-width:800px){.grid{grid-template-columns:1fr}}h1,h2,h3{margin-top:0}
</style></head><body><main>
""");
        builder.Append($"<h1>Báo cáo kiểm thử API</h1><div class='card summary'><div><div class='muted'>Dự án</div><div class='metric'>{E(project.Name)}</div></div><div><div class='muted'>Môi trường</div><div class='metric'>{E(environment)}</div></div><div><div class='muted'>Thời gian bắt đầu</div><div>{E(startedAt.ToString("dd/MM/yyyy HH:mm:ss zzz"))}</div></div><div><div class='muted'>Tổng số</div><div class='metric'>{results.Count}</div></div><div><div class='muted'>Thành công</div><div class='metric pass'>{passed}</div></div><div><div class='muted'>Thất bại</div><div class='metric fail'>{results.Count-passed}</div></div></div>");
        foreach (var test in results)
        {
            builder.Append($"<details {(test.Passed ? "" : "open")}><summary><span class='{(test.Passed ? "pass" : "fail")}'>{(test.Passed ? "THÀNH CÔNG" : "THẤT BẠI")}</span> · {E(test.Id)} — {E(test.Name)} <span class='muted'>({test.Duration.TotalMilliseconds:F0} ms)</span></summary>");
            if (test.Error is not null) builder.Append($"<pre class='fail'>{E(test.Error)}</pre>");
            foreach (var step in test.Steps)
            {
                builder.Append($"<details {(step.Passed ? "" : "open")}><summary><span class='{(step.Passed ? "pass" : "fail")}'>{(step.Passed ? "THÀNH CÔNG" : "THẤT BẠI")}</span> · {(step.Cleanup ? "Dọn dữ liệu: " : "")}{E(step.Name)} <span class='muted'>({step.Duration.TotalMilliseconds:F0} ms)</span></summary>");
                builder.Append($"<p><span class='badge'>{E(step.Method)}</span><code>{E(step.Path)}</code></p><div class='grid'><section><h3>Dữ liệu gửi đi</h3><pre>{E(step.Payload ?? "(không có dữ liệu gửi đi)")}</pre></section><section><h3>Kết quả mong đợi</h3><pre>{E(step.Expected)}</pre></section><section><h3>Kết quả thực tế</h3><pre>Mã trạng thái HTTP: {E(step.ActualStatus?.ToString() ?? "Không nhận được phản hồi")}\n\n{E(step.ActualResponse ?? "(phản hồi không có nội dung)")}</pre></section><section><h3>Kết luận</h3><pre class='{(step.Passed ? "pass" : "fail")}'>{E(step.Passed ? "THÀNH CÔNG" : step.Error ?? "THẤT BẠI")}</pre></section></div></details>");
            }
            builder.Append("</details>");
        }
        builder.Append("</main></body></html>");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
