using System.Text.Json;
using AutoTest.UnitX;

namespace AutoTest.Web;

public sealed class UnitXWorkspaceStore
{
    private readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly WorkspacePersistence persistence;
    private readonly string root;

    public UnitXWorkspaceStore(string artifactsRoot, WorkspacePersistence? persistence = null)
    {
        root = Path.Combine(Path.GetFullPath(artifactsRoot), "unitx");
        this.persistence = persistence ?? new WorkspacePersistence();
    }

    public async Task<UnitXWorkspace> CreateAsync(string project, CancellationToken cancellationToken)
    {
        UnitXWorkspace workspace = new(Guid.NewGuid().ToString("N"), Project(project), 1, DateTimeOffset.UtcNow,
            new DecisionTable("Bảng quyết định mới", [], []));
        return await SaveAsync(workspace, null, cancellationToken);
    }

    public async Task<UnitXWorkspace> LoadAsync(string project, string id, CancellationToken cancellationToken)
    {
        string path = PathFor(project, id);
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy workspace UnitX.");
        await using FileStream stream = File.OpenRead(path);
        UnitXWorkspace? workspace = await JsonSerializer.DeserializeAsync<UnitXWorkspace>(stream, options, cancellationToken);
        if (workspace is null || !workspace.Project.Equals(Project(project), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Workspace UnitX không hợp lệ.");
        return workspace;
    }

    public async Task<UnitXWorkspace> SaveAsync(UnitXWorkspace workspace, int? expectedRevision, CancellationToken cancellationToken)
    {
        string path = PathFor(workspace.Project, workspace.Id);
        return await persistence.WithWorkspaceLockAsync(path, async token =>
        {
            int current = File.Exists(path) ? (await LoadAsync(workspace.Project, workspace.Id, token)).Revision : 0;
            if (expectedRevision is not null && expectedRevision != current) throw new ApiBuilderWorkspaceConflictException();

            UnitXWorkspace saved = workspace with { Revision = current + 1, UpdatedAt = DateTimeOffset.UtcNow };
            await WorkspacePersistence.WriteAsync(path, JsonSerializer.Serialize(saved, options), token);
            return saved;
        }, cancellationToken);
    }

    private string PathFor(string project, string id) => Path.Combine(root, Project(project), Id(id), "workspace.json");
    private static string Project(string value) => string.IsNullOrWhiteSpace(value) || value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? throw new InvalidDataException("Tên project không hợp lệ.") : value.Trim();
    private static string Id(string value) => value.Length == 32 && value.All(char.IsAsciiHexDigit) ? value : throw new InvalidDataException("Workspace ID không hợp lệ.");
}
public sealed record UnitXWorkspace(string Id, string Project, int Revision, DateTimeOffset UpdatedAt, DecisionTable Table);
