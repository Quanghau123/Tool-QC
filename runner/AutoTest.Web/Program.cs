using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseUrls(builder.Configuration["url"] ?? "http://127.0.0.1:5080");
WebApplication app = builder.Build();
string root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
ConcurrentDictionary<string, RunState> runs = new(StringComparer.OrdinalIgnoreCase);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/projects", () =>
{
    string projectsRoot = Path.Combine(root, "projects");
    if (!Directory.Exists(projectsRoot)) return Array.Empty<object>();
    return Directory.EnumerateDirectories(projectsRoot)
        .Where(path => File.Exists(Path.Combine(path, "project.json")))
        .Select(path => new { name = Path.GetFileName(path) })
        .OrderBy(project => project.name)
        .Cast<object>()
        .ToArray();
});

app.MapGet("/api/projects/{project}/tests", (string project) =>
{
    string projectDir = SafeProjectDirectory(root, project);
    string projectName = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectDir, "project.json")))
        .RootElement.GetProperty("name").GetString()!;
    return ReadTests(Path.Combine(projectDir, "testcases"), projectName)
        .Select(test => new
        {
            test.Id, test.Name, test.Tags, test.Destructive, test.Group, test.StepCount,
        })
        .OrderBy(test => test.Group)
        .ThenBy(test => test.Name)
        .ToArray();
});

app.MapPost("/api/runs", (RunRequest request) =>
{
    _ = SafeProjectDirectory(root, request.Project);
    string runId = Guid.NewGuid().ToString("N");
    RunState state = new(runId, request.Project, request.Tags ?? [], DateTimeOffset.Now);
    runs[runId] = state;
    _ = Task.Run(() => RunAsync(root, state));
    return Results.Accepted($"/api/runs/{runId}", new { runId });
});

app.MapGet("/api/runs/{runId}", (string runId) =>
    runs.TryGetValue(runId, out RunState? state) ? Results.Ok(state.Snapshot()) : Results.NotFound());

app.MapGet("/api/reports/{runId}", (string runId) =>
{
    if (!runs.TryGetValue(runId, out RunState? state) || string.IsNullOrWhiteSpace(state.ReportPath))
        return Results.NotFound();
    string report = Path.GetFullPath(state.ReportPath);
    string resultsRoot = Path.GetFullPath(Path.Combine(root, ".artifacts"));
    string configuredResultsRoot = Path.GetFullPath(Path.Combine(root, "test-results"));
    if (!report.StartsWith(resultsRoot, StringComparison.OrdinalIgnoreCase)
        && !report.StartsWith(configuredResultsRoot, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest();
    return Results.File(report, "text/html");
});

app.MapFallbackToFile("index.html");
app.Run();

static string SafeProjectDirectory(string root, string project)
{
    if (string.IsNullOrWhiteSpace(project) || project.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        throw new BadHttpRequestException("Tên project không hợp lệ.");
    string projectsRoot = Path.GetFullPath(Path.Combine(root, "projects"));
    string projectDir = Path.GetFullPath(Path.Combine(projectsRoot, project));
    if (!projectDir.StartsWith(projectsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || !File.Exists(Path.Combine(projectDir, "project.json")))
        throw new BadHttpRequestException("Không tìm thấy project.");
    return projectDir;
}

static IEnumerable<TestItem> ReadTests(string directory, string project)
{
    foreach (string path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        JsonElement root = document.RootElement;
        if (!string.Equals(root.GetProperty("project").GetString(), project, StringComparison.OrdinalIgnoreCase)) continue;
        string relative = Path.GetRelativePath(directory, path);
        string group = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        foreach (JsonElement test in root.GetProperty("cases").EnumerateArray())
        {
            string[] tags = test.TryGetProperty("tags", out JsonElement tagsElement)
                ? tagsElement.EnumerateArray().Select(tag => tag.GetString()!).ToArray()
                : [];
            yield return new(
                test.GetProperty("id").GetString()!,
                test.GetProperty("name").GetString()!,
                tags,
                test.TryGetProperty("destructive", out JsonElement destructive) && destructive.GetBoolean(),
                group,
                test.GetProperty("steps").GetArrayLength());
        }
    }
}

static async Task RunAsync(string root, RunState state)
{
    try
    {
        List<string> arguments = [
            "run", "--project", Path.Combine(root, "runner", "AutoTest.Runner", "AutoTest.Runner.csproj"),
            "-c", "Release", "--no-build", "--", "--project", state.Project,
        ];
        if (state.Tags.Length > 0) arguments.AddRange(["--tags", string.Join(',', state.Tags)]);
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = startInfo };
        process.OutputDataReceived += (_, eventArgs) => state.AddLine(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => state.AddLine(eventArgs.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        state.Complete(process.ExitCode);
    }
    catch (Exception exception)
    {
        state.Fail(exception.Message);
    }
}

sealed record RunRequest(string Project, string[]? Tags);
sealed record TestItem(string Id, string Name, string[] Tags, bool Destructive, string Group, int StepCount);

sealed class RunState(string id, string project, string[] tags, DateTimeOffset startedAt)
{
    private readonly object gate = new();
    private readonly List<string> lines = [];
    public string Id { get; } = id;
    public string Project { get; } = project;
    public string[] Tags { get; } = tags;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public string Status { get; private set; } = "running";
    public int? ExitCode { get; private set; }
    public string? ReportPath { get; private set; }

    public void AddLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (gate)
        {
            lines.Add(line);
            const string marker = "Báo cáo HTML:";
            int index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) ReportPath = line[(index + marker.Length)..].Trim();
        }
    }

    public void Complete(int exitCode)
    {
        lock (gate) { ExitCode = exitCode; Status = exitCode == 0 ? "passed" : "failed"; }
    }

    public void Fail(string message)
    {
        lock (gate) { lines.Add(message); ExitCode = -1; Status = "failed"; }
    }

    public object Snapshot()
    {
        lock (gate)
            return new { Id, Project, Tags, StartedAt, Status, ExitCode, ReportPath, lines = lines.ToArray() };
    }
}
