using System.Text.Json;

namespace AutoTest.Web;

public sealed class ApiBuilderProjectConfiguration
{
    private ApiBuilderProjectConfiguration(
        string projectDirectory,
        string? openApiPath,
        string? sourcePath,
        string? localSourcePath)
    {
        ProjectDirectory = projectDirectory;
        OpenApiPath = openApiPath;
        SourcePath = sourcePath;
        LocalSourcePath = localSourcePath;
    }

    public string ProjectDirectory { get; }
    public string? OpenApiPath { get; }
    public string? SourcePath { get; }
    public string? LocalSourcePath { get; }

    public static ApiBuilderProjectConfiguration Load(string projectDirectory)
    {
        string projectFile = Path.Combine(projectDirectory, "project.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(projectFile));
        JsonElement? apiBuilder = document.RootElement.TryGetProperty("apiBuilder", out JsonElement value)
            && value.ValueKind != JsonValueKind.Null
            ? value
            : null;
        if (apiBuilder is { ValueKind: not JsonValueKind.Object })
            throw new InvalidDataException("Cấu hình apiBuilder không hợp lệ.");

        return new ApiBuilderProjectConfiguration(
            projectDirectory,
            apiBuilder is null ? null : ReadPath(apiBuilder.Value, "openApiPath"),
            apiBuilder is null ? null : ReadPath(apiBuilder.Value, "sourcePath"),
            ReadLocalSourcePath(projectDirectory));
    }

    public static void SaveLocalSourcePath(string projectDirectory, string localSourcePath)
    {
        string sourceDirectory = ResolveLocalDirectory(localSourcePath);
        string localFile = Path.Combine(projectDirectory, "project.local.json");
        string temporaryFile = Path.Combine(
            projectDirectory,
            $".{Path.GetFileName(localFile)}.{Guid.NewGuid():N}.tmp");
        string content = JsonSerializer.Serialize(new
        {
            apiBuilder = new { localSourcePath = sourceDirectory }
        });

        try
        {
            File.WriteAllText(temporaryFile, content);
            File.Move(temporaryFile, localFile, true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
        }
    }

    public string? ResolveOpenApiPath() => ResolvePath(OpenApiPath, false);

    public string? ResolveSourcePath() => ResolvePath(SourcePath, true);

    public string? ResolveLocalSourcePath()
        => string.IsNullOrWhiteSpace(LocalSourcePath)
            ? null
            : ResolveLocalDirectory(LocalSourcePath);

    public static IReadOnlyList<string> FindOpenApiCandidates(string sourceDirectory)
    {
        string root = ResolveLocalDirectory(sourceDirectory);
        List<string> candidates = [];
        Stack<string> directories = new([root]);
        int visitedDirectories = 0;
        while (directories.Count > 0 && candidates.Count < 32 && visitedDirectories < 256)
        {
            string directory = directories.Pop();
            visitedDirectories++;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                DirectoryInfo info = new(child);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0
                    && !IsExcludedSourceDirectory(info.Name))
                    directories.Push(child);
            }
            foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0
                    && IsOpenApiCandidate(Path.GetFileName(file)))
                    candidates.Add(file);
                if (candidates.Count == 32)
                    break;
            }
        }
        return candidates.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsDotNetSource(string sourceDirectory)
    {
        Stack<string> directories = new([ResolveLocalDirectory(sourceDirectory)]);
        int visitedDirectories = 0;
        while (directories.Count > 0 && visitedDirectories < 256)
        {
            string directory = directories.Pop();
            visitedDirectories++;
            if (Directory.EnumerateFiles(directory, "*.csproj").Any())
                return true;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                DirectoryInfo info = new(child);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0
                    && !IsExcludedSourceDirectory(info.Name))
                    directories.Push(child);
            }
        }
        return false;
    }

    private static string? ReadPath(JsonElement apiBuilder, string propertyName)
    {
        if (!apiBuilder.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind == JsonValueKind.Null)
            return null;
        if (property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Cấu hình apiBuilder.{propertyName} phải là chuỗi.");
        return property.GetString();
    }

    private static string? ReadLocalSourcePath(string projectDirectory)
    {
        string localFile = Path.Combine(projectDirectory, "project.local.json");
        if (!File.Exists(localFile))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(localFile));
            if (!document.RootElement.TryGetProperty("apiBuilder", out JsonElement apiBuilder)
                || apiBuilder.ValueKind == JsonValueKind.Null)
                return null;
            if (apiBuilder.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Cấu hình local apiBuilder không hợp lệ.");
            return ReadPath(apiBuilder, "localSourcePath");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveLocalDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathRooted(path)
            || Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !uri.IsFile
            || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Đường dẫn source local phải là thư mục local hợp lệ.");

        string fullPath = Path.GetFullPath(path);
        if (!Path.IsPathRooted(fullPath)
            || !Directory.Exists(fullPath))
            throw new InvalidDataException("Thư mục source local không tồn tại.");

        FileAttributes attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Thư mục source local không được là reparse point.");
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private string? ResolvePath(string? path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Path.IsPathRooted(path)
            || Uri.TryCreate(path, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            throw new InvalidDataException("Đường dẫn contract phải là đường dẫn tương đối trong project.");

        string projectRoot = Path.GetFullPath(ProjectDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
        if (!fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || IsEnvironmentPath(fullPath))
            throw new InvalidDataException("Đường dẫn contract nằm ngoài phạm vi project cho phép.");
        if (directory ? !Directory.Exists(fullPath) : !File.Exists(fullPath))
            throw new InvalidDataException(directory
                ? "Thư mục source contract không tồn tại."
                : "Tệp OpenAPI contract không tồn tại.");
        return fullPath;
    }

    private static bool IsEnvironmentPath(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals(".env", StringComparison.OrdinalIgnoreCase));

    private static bool IsExcludedSourceDirectory(string name)
        => name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".artifacts", StringComparison.OrdinalIgnoreCase)
            || name.Equals("test-results", StringComparison.OrdinalIgnoreCase)
            || name.Equals("logs", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenApiCandidate(string name)
        => name.Equals("openapi.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("swagger.json", StringComparison.OrdinalIgnoreCase);
}
