// CLI generator: quét controller/source và tạo testcase nháp để người dùng duyệt.
using System.Text.Json;
using System.Text.RegularExpressions;

string Arg(string key, string fallback = "")
{
    int index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

string source = Path.GetFullPath(Arg("--source"));
string project = Arg("--project");
string output = Path.GetFullPath(Arg("--output", Path.Combine("projects", project, "testcases", "generated", "source-draft.json")));
if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(project))
    throw new InvalidOperationException("Dùng --source <source> --project <project> [--output <file>].");

HashSet<(string Method, string Path)> routes = [];
foreach (string file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
{
    string text = File.ReadAllText(file);
    string prefix = Regex.Match(text, "\\[Route\\(\\\"([^\\\"]+)\\\"\\)\\]").Groups[1].Value;
    foreach (Match match in Regex.Matches(text, "\\[(HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch)(?:\\(\\\"([^\\\"]*)\\\"\\))?\\]"))
    {
        string method = match.Groups[1].Value[4..].ToUpperInvariant();
        string suffix = match.Groups[2].Value;
        string path = ("/" + prefix + "/" + suffix).Replace("//", "/")
            .Replace("[controller]", Path.GetFileNameWithoutExtension(file).Replace("Controller", ""), StringComparison.OrdinalIgnoreCase);
        routes.Add((method, path));
    }
}

var cases = routes.OrderBy(route => route.Path).ThenBy(route => route.Method).Select(route => new
{
    id = $"generated.{Slug(route.Method + "-" + route.Path)}",
    name = $"Bản nháp kiểm tra {route.Method} {route.Path}",
    tags = new[] { "generated-draft" },
    destructive = route.Method != "GET",
    disabled = true,
    draft = true,
    coverage = new
    {
        sourceVerification = new { files = new[] { source }, verifiedAt = DateTimeOffset.Now.ToString("O"), contractVersion = "source-scan-draft" },
        contract = new { status = "covered", evidenceSteps = new[] { "Gọi endpoint theo source" } },
        validation = new { status = "blocked", reason = "Cần người duyệt bổ sung boundary" },
        authentication = new { status = "blocked", reason = "Cần người duyệt xác minh auth" },
        scope = new { status = "blocked", reason = "Cần người duyệt xác minh scope" },
        integrity = new { status = "blocked", reason = "Cần người duyệt xác minh database" },
        lifecycle = new { status = "notApplicable", reason = "Chưa phát hiện từ static scan" },
        concurrency = new { status = "notApplicable", reason = "Chưa phát hiện từ static scan" },
        reporting = new { status = "covered", evidenceSteps = new[] { "Gọi endpoint theo source" } }
    },
    steps = new[] { new { name = "Gọi endpoint theo source", request = new { method = route.Method, path = route.Path }, expect = new { statusOneOf = new[] { 200, 201, 202, 204, 400, 401, 403, 404 } } } }
}).ToArray();

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 2, project, generatedDraft = true, disabled = true, cases }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Đã tạo {cases.Length} testcase bản nháp: {output}");
Console.WriteLine("File draft đang disabled và phải được người kiểm thử duyệt, chuyển sang file testcase chính thức trước khi chạy.");

static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
