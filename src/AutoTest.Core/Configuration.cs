using System.Text.Json;
namespace AutoTest.Core;
// Nhóm cấu hình: đọc .env và ưu tiên biến môi trường hệ điều hành.
public sealed class EnvironmentStore : IEnvironmentStore
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    public static EnvironmentStore Load(string path) => Load([path]);
    public static EnvironmentStore Load(IEnumerable<string> paths) { var s = new EnvironmentStore(); foreach (var path in paths) s.LoadFile(path); return s; }
    private void LoadFile(string path) { if (!File.Exists(path)) return; foreach (var raw in File.ReadLines(path)) { var line = raw.Trim(); if (line.Length == 0 || line.StartsWith('#')) continue; var i = line.IndexOf('='); if (i > 0) values[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"'); } }
    public string? Get(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : values.GetValueOrDefault(key);
    public string Require(string key) => Get(key) is { Length: > 0 } v ? v : throw new InvalidOperationException($"Thiếu cấu hình bắt buộc: {key}");
    public bool Bool(string key, bool fallback = false) => bool.TryParse(Get(key), out var v) ? v : fallback;
    public int Int(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;
}
// Nhóm nạp đặc tả: lỗi project.json, testcase JSON hoặc project mismatch kiểm tra tại đây.
public static class SpecLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
    public static ProjectSpec Project(string path) => Read<ProjectSpec>(path);
    public static ErrorCatalogueSpec ErrorCatalogue(string projectDirectory, ProjectSpec project)
    {
        if (project.ErrorCatalogue is null) throw new InvalidDataException("Project chưa khai báo error catalogue.");
        string projectRoot = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string cataloguePath = Path.GetFullPath(Path.Combine(projectDirectory, project.ErrorCatalogue.Path));
        if (!cataloguePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Đường dẫn error catalogue nằm ngoài project.");
        ErrorCatalogueSpec catalogue = Read<ErrorCatalogueSpec>(cataloguePath);
        ValidateCatalogue(catalogue);
        return catalogue;
    }
    private static void ValidateCatalogue(ErrorCatalogueSpec catalogue)
    {
        if (catalogue.SchemaVersion != 1) throw new InvalidDataException($"Phiên bản error catalogue không hỗ trợ: {catalogue.SchemaVersion}");
        if (!System.Text.RegularExpressions.Regex.IsMatch(catalogue.Prefix, "^[A-Z][A-Z0-9]{1,15}$")) throw new InvalidDataException("Prefix error catalogue không hợp lệ.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (ErrorCatalogueEntrySpec entry in catalogue.Entries)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Code, $"^{catalogue.Prefix}-[A-Z][A-Z0-9]*-\\d{{3}}$")) throw new InvalidDataException($"Mã error catalogue không hợp lệ: {entry.Code}");
            if (!codes.Add(entry.Code)) throw new InvalidDataException($"Mã error catalogue bị trùng: {entry.Code}");
            if (entry.HttpStatus is < 100 or > 599) throw new InvalidDataException($"HTTP status error catalogue không hợp lệ: {entry.HttpStatus}");
            if (string.IsNullOrWhiteSpace(entry.Messages.Vietnamese) || string.IsNullOrWhiteSpace(entry.Messages.English)) throw new InvalidDataException($"Error catalogue thiếu bản dịch: {entry.Code}");
        }
    }
    public static IEnumerable<CaseSpec> Cases(string dir, string project) { foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)) { var suite = Read<SuiteSpec>(path); if (!suite.Project.Equals(project, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Project mismatch: {path}"); var relative = Path.GetRelativePath(dir, path); var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); var sourceGroup = segments.Length > 1 ? segments[0] : "_root"; var schema = suite.SchemaVersion ?? 2; if (schema is < 1 or > 2) throw new InvalidDataException($"schemaVersion không hỗ trợ tại {path}: {schema}"); foreach (var c in suite.Cases) { if (string.IsNullOrWhiteSpace(c.Id) || c.Steps.Count == 0) throw new InvalidDataException($"Invalid case: {path}"); yield return c with { SourceGroup = sourceGroup, SchemaVersion = schema }; } } }
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new InvalidDataException($"Invalid JSON: {path}");
}
