using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoTest.Web;

public sealed class ApiBuilderWorkspaceStore
{
    private static readonly HashSet<string> ReservedVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "unique", "shortUnique", "nowIso", "futureStartIso", "futureEndIso", "authToken"
    };

    private readonly string artifactsRoot;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly TimeSpan retention;

    public ApiBuilderWorkspaceStore(string artifactsRoot, TimeSpan? retention = null)
    {
        this.artifactsRoot = Path.GetFullPath(artifactsRoot);
        this.retention = retention ?? TimeSpan.FromDays(14);
    }

    public async Task<ApiBuilderWorkspace> CreateAsync(string project, string? owner, CancellationToken cancellationToken)
    {
        string workspaceId = Guid.NewGuid().ToString("N");
        ApiBuilderWorkspace workspace = new(
            workspaceId,
            SafeProject(project),
            string.IsNullOrWhiteSpace(owner) ? "local" : owner.Trim(),
            1,
            DateTimeOffset.UtcNow,
            null,
            "not-run",
            [],
            []);
        return await SaveAsync(workspace, null, cancellationToken);
    }

    public async Task<ApiBuilderWorkspace> LoadAsync(string project, string workspaceId, CancellationToken cancellationToken)
    {
        string path = WorkspacePath(project, workspaceId);
        if (!File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy workspace API Builder.");

        await using FileStream stream = File.OpenRead(path);
        ApiBuilderWorkspace? workspace = await JsonSerializer.DeserializeAsync<ApiBuilderWorkspace>(stream, jsonOptions, cancellationToken);
        if (workspace is null || !workspace.Project.Equals(SafeProject(project), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Workspace API Builder bị hỏng hoặc không thuộc project đã chọn.");
        workspace = workspace with
        {
            Variables = workspace.Variables ?? [],
            Steps = workspace.Steps ?? []
        };
        Validate(workspace);
        return workspace;
    }

    public async Task<ApiBuilderWorkspace> SaveAsync(ApiBuilderWorkspace workspace, int? expectedRevision, CancellationToken cancellationToken)
    {
        Validate(workspace);
        string path = WorkspacePath(workspace.Project, workspace.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int currentRevision = File.Exists(path) ? (await LoadAsync(workspace.Project, workspace.Id, cancellationToken)).Revision : 0;
        if (expectedRevision is not null && expectedRevision != currentRevision)
            throw new ApiBuilderWorkspaceConflictException();

        ApiBuilderWorkspace saved = workspace with { Revision = currentRevision + 1, UpdatedAt = DateTimeOffset.UtcNow };
        string temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(saved, jsonOptions), cancellationToken);
        File.Move(temporaryPath, path, true);
        return saved;
    }

    public static ApiBuilderWorkspace AddVariable(ApiBuilderWorkspace workspace, ApiBuilderVariable variable)
    {
        ValidateVariable(variable);
        if (workspace.Variables.Any(item => item.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Tên biến đã tồn tại trong workspace.");
        return workspace with { Variables = workspace.Variables.Append(variable with { Name = variable.Name.Trim() }).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray() };
    }

    public int DeleteExpiredWorkspaces(DateTimeOffset now)
    {
        string directory = Path.Combine(artifactsRoot, "api-builder");
        if (!Directory.Exists(directory))
            return 0;

        int deleted = 0;
        foreach (string workspacePath in Directory.EnumerateFiles(directory, "workspace.json", SearchOption.AllDirectories))
        {
            if (File.GetLastWriteTimeUtc(workspacePath) > now.UtcDateTime - retention)
                continue;
            Directory.Delete(Path.GetDirectoryName(workspacePath)!, true);
            deleted++;
        }
        return deleted;
    }

    public static string RedactPreview(string fieldName, string? preview)
        => IsSensitive(fieldName) ? "***" : preview ?? string.Empty;

    private static void Validate(ApiBuilderWorkspace workspace)
    {
        _ = SafeProject(workspace.Project);
        if (!IsWorkspaceId(workspace.Id))
            throw new InvalidDataException("Workspace ID không hợp lệ.");
        if (string.IsNullOrWhiteSpace(workspace.Owner) || workspace.Revision < 0)
            throw new InvalidDataException("Metadata workspace không hợp lệ.");
        foreach (ApiBuilderVariable variable in workspace.Variables)
            ValidateVariable(variable);
        if (workspace.Variables.GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidDataException("Tên biến bị trùng trong workspace.");
        foreach (ApiBuilderStep step in workspace.Steps)
            ValidateStep(step);
    }

    private static void ValidateStep(ApiBuilderStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Name)
            || string.IsNullOrWhiteSpace(step.Method)
            || !step.Path.StartsWith('/'))
            throw new InvalidDataException("Step API Builder không hợp lệ.");
        if (step.RequestFields is null || step.ResponseFields is null)
            throw new InvalidDataException("Field của step API Builder không hợp lệ.");
        foreach (ApiBuilderField field in step.RequestFields.Concat(step.ResponseFields))
            if (string.IsNullOrWhiteSpace(field.Name) || !field.JsonPath.StartsWith('$'))
                throw new InvalidDataException("Field API Builder không hợp lệ.");
    }

    private static void ValidateVariable(ApiBuilderVariable variable)
    {
        if (string.IsNullOrWhiteSpace(variable.Name) || variable.Name.Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
            throw new InvalidDataException("Tên biến chỉ gồm chữ, số hoặc dấu gạch dưới.");
        if (ReservedVariableNames.Contains(variable.Name))
            throw new InvalidDataException("Tên biến trùng với biến hệ thống.");
        if (string.IsNullOrWhiteSpace(variable.SourceStep) || !variable.JsonPath.StartsWith('$'))
            throw new InvalidDataException("Nguồn biến hoặc JSONPath không hợp lệ.");
    }

    private string WorkspacePath(string project, string workspaceId)
        => Path.Combine(artifactsRoot, "api-builder", SafeProject(project), SafeWorkspaceId(workspaceId), "workspace.json");

    private static string SafeProject(string project)
    {
        if (string.IsNullOrWhiteSpace(project) || project.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || project.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Tên project không hợp lệ.");
        return project.Trim();
    }

    private static string SafeWorkspaceId(string workspaceId)
    {
        if (!IsWorkspaceId(workspaceId))
            throw new InvalidDataException("Workspace ID không hợp lệ.");
        return workspaceId;
    }

    private static bool IsWorkspaceId(string value) => value.Length == 32 && value.All(character => char.IsAsciiHexDigit(character));

    private static bool IsSensitive(string fieldName) => new[] { "password", "confirmPassword", "token", "accessToken", "refreshToken", "authorization", "secret", "connectionString" }
        .Any(name => fieldName.Contains(name, StringComparison.OrdinalIgnoreCase));
}

public sealed class ApiBuilderWorkspaceConflictException : Exception
{
    public ApiBuilderWorkspaceConflictException() : base("Workspace đã được cập nhật ở phiên khác. Hãy tải lại trước khi lưu.") { }
}

public sealed record ApiBuilderWorkspace(
    string Id,
    string Project,
    string Owner,
    int Revision,
    DateTimeOffset UpdatedAt,
    string? SourceFingerprint,
    string PreflightStatus,
    IReadOnlyList<ApiBuilderVariable> Variables,
    IReadOnlyList<ApiBuilderStep> Steps);

public sealed record ApiBuilderStep(
    string Name,
    string Method,
    string Path,
    IReadOnlyList<ApiBuilderField> RequestFields,
    IReadOnlyList<ApiBuilderField> ResponseFields);

public sealed record ApiBuilderField(
    string Name,
    string JsonPath,
    string Type,
    string Value);

public sealed record ApiBuilderVariable(
    string Name,
    string SourceStep,
    string JsonPath,
    string Type,
    string Preview,
    DateTimeOffset CreatedAt);
