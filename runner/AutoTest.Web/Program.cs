using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using AutoTest.MessageScanner;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseUrls(builder.Configuration["url"] ?? "http://127.0.0.1:5080");
WebApplication app = builder.Build();
string root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
string historyDirectory = Path.Combine(root, ".artifacts", "ui-runs");
Directory.CreateDirectory(historyDirectory);
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

app.MapGet("/api/projects/{project}/environment", (string project) =>
{
    string projectDir = SafeProjectDirectory(root, project);
    return ReadEnvironmentConfiguration(root, projectDir);
});

app.MapPut("/api/projects/{project}/environment", async (string project, EnvironmentUpdate request) =>
{
    string projectDir = SafeProjectDirectory(root, project);
    HashSet<string> allowed = ReadEnvironmentConfiguration(root, projectDir)
        .SelectMany(group => group.Variables)
        .Select(variable => variable.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string> updates = new(StringComparer.OrdinalIgnoreCase);
    foreach ((string key, string value) in request.Values)
    {
        if (!allowed.Contains(key) || !IsValidEnvironmentKey(key))
            return Results.BadRequest(new { message = $"Biến môi trường không hợp lệ: {key}" });
        updates[key] = value ?? string.Empty;
    }
    WriteEnvironmentOverrides(Path.Combine(projectDir, ".env"), updates);
    await Task.CompletedTask;
    return Results.Ok(new { message = "Đã lưu cấu hình ghi đè cho dự án.", updated = updates.Count });
});

app.MapPost("/api/projects/{project}/environment/check", async (string project) =>
{
    string projectDir = SafeProjectDirectory(root, project);
    return Results.Ok(await UiSupport.CheckConnectionsAsync(root, projectDir));
});

app.MapPost("/api/projects/{project}/generate", async (string project, GenerateRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { message = "Đường dẫn source không tồn tại." });
    string output = Path.Combine(root, ".artifacts", "generated-testcases", project, $"source-draft-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    ProcessStartInfo info = new("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (string argument in new[] { "run", "--project", Path.Combine(root, "runner", "AutoTest.Generator", "AutoTest.Generator.csproj"), "-c", "Release", "--", "--source", request.SourcePath, "--project", project, "--output", output }) info.ArgumentList.Add(argument);
    using Process process = new() { StartInfo = info }; process.Start();
    string standardOutput = await process.StandardOutput.ReadToEndAsync(); string standardError = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
    return process.ExitCode == 0 ? Results.Ok(new { output, message = standardOutput.Trim() }) : Results.BadRequest(new { message = standardError.Trim() });
});

app.MapPost("/api/message-scans", (MessageScanRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { message = "Đường dẫn source không tồn tại." });

    string sourcePath = Path.GetFullPath(request.SourcePath);
    MessageScanResult scan = new SourceMessageScanner().Scan(sourcePath);
    string scanId = Guid.NewGuid().ToString("N");
    string output = Path.Combine(root, ".artifacts", "message-scans",
        $"{Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}-messages-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    string report = new MessageWorkbookWriter().Write(output, scan);
    UiSupport.SaveMessageScan(root, scanId, report);

    return Results.Ok(new
    {
        scanId,
        sourceDirectory = scan.SourceDirectory,
        scan.ScannedFileCount,
        messageCount = scan.Messages.Count,
        moduleCount = scan.Messages.Select(message => message.Module).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        skippedFileCount = scan.SkippedFiles.Count,
        modules = scan.Messages.GroupBy(message => message.Module).OrderBy(group => group.Key).Select(group => new
        {
            name = group.Key,
            count = group.Count(),
            messages = group.Select(message => new { key = message.Key, vietnamese = message.Vietnamese, english = message.English, sourceFiles = message.SourceFiles })
        })
    });
});

app.MapGet("/api/message-scans/{scanId}/excel", (string scanId) =>
{
    string? report = UiSupport.ReadMessageScan(root, scanId);
    return report is not null && File.Exists(report)
        ? Results.File(report, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Path.GetFileName(report))
        : Results.NotFound();
});

app.MapPost("/api/runs", (RunRequest request) =>
{
    _ = SafeProjectDirectory(root, request.Project);
    string runId = Guid.NewGuid().ToString("N");
    if (request.CaseIds is not { Length: > 0 })
        return Results.BadRequest(new { message = "Hãy chọn ít nhất một testcase." });
    RunState state = new(runId, request.Project, request.CaseIds, DateTimeOffset.Now, historyDirectory);
    runs[runId] = state;
    _ = Task.Run(() => RunAsync(root, state));
    return Results.Accepted($"/api/runs/{runId}", new { runId });
});

app.MapGet("/api/runs/{runId}", (string runId) =>
    runs.TryGetValue(runId, out RunState? state) ? Results.Ok(state.Snapshot())
        : UiSupport.ReadHistory(historyDirectory, runId) is { } saved ? Results.Ok(saved) : Results.NotFound());

app.MapGet("/api/runs", () => UiSupport.ReadHistory(historyDirectory));

app.MapDelete("/api/runs/{runId}", (string runId) =>
    runs.TryGetValue(runId, out RunState? state) && state.Cancel()
        ? Results.Ok(new { message = "Đã dừng workload." })
        : Results.NotFound(new { message = "Không tìm thấy workload đang chạy." }));

app.MapGet("/api/reports/{runId}", (string runId) =>
{
    string? reportPath = runs.TryGetValue(runId, out RunState? state)
        ? state.ReportPath : UiSupport.ReadHistory(historyDirectory, runId)?.ReportPath;
    if (string.IsNullOrWhiteSpace(reportPath))
        return Results.NotFound();
    string report = Path.GetFullPath(reportPath);
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

static EnvironmentGroup[] ReadEnvironmentConfiguration(string root, string projectDir)
{
    string rootExample = Path.Combine(root, ".env.example");
    string projectExample = Path.Combine(projectDir, ".env.example");
    Dictionary<string, EnvironmentDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
    ReadDefinitions(rootExample, "Dùng chung", definitions);
    ReadDefinitions(projectExample, "Dự án", definitions);
    Dictionary<string, string> rootValues = ReadEnvironmentFile(Path.Combine(root, ".env"));
    Dictionary<string, string> projectValues = ReadEnvironmentFile(Path.Combine(projectDir, ".env"));

    return definitions.Values
        .GroupBy(definition => $"{definition.Scope} · {definition.Group}")
        .Select(group => new EnvironmentGroup(
            group.Key,
            group.Select(definition =>
            {
                string? systemValue = Environment.GetEnvironmentVariable(definition.Key);
                string source;
                string value;
                if (!string.IsNullOrEmpty(systemValue)) { source = "Hệ thống"; value = systemValue; }
                else if (projectValues.TryGetValue(definition.Key, out string? projectValue)) { source = "Dự án .env"; value = projectValue; }
                else if (rootValues.TryGetValue(definition.Key, out string? rootValue)) { source = ".env dùng chung"; value = rootValue; }
                else { source = "Chưa cấu hình"; value = definition.DefaultValue; }
                bool secret = IsSecret(definition.Key);
                return new EnvironmentVariable(definition.Key, secret ? Mask(value) : value, source, secret, !string.IsNullOrWhiteSpace(value));
            }).OrderBy(variable => variable.Key).ToArray()))
        .OrderBy(group => group.Name)
        .ToArray();
}

static void ReadDefinitions(string path, string scope, Dictionary<string, EnvironmentDefinition> definitions)
{
    if (!File.Exists(path)) return;
    string group = "Khác";
    foreach (string raw in File.ReadLines(path))
    {
        string line = raw.Trim();
        if (line.StartsWith('#')) { group = line.TrimStart('#').Trim(); continue; }
        int separator = line.IndexOf('=');
        if (separator <= 0) continue;
        string key = line[..separator].Trim();
        definitions[key] = new(key, group, scope, line[(separator + 1)..].Trim().Trim('"'));
    }
}

static Dictionary<string, string> ReadEnvironmentFile(string path)
{
    Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(path)) return values;
    foreach (string raw in File.ReadLines(path))
    {
        string line = raw.Trim();
        int separator = line.IndexOf('=');
        if (separator <= 0 || line.StartsWith('#')) continue;
        values[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
    }
    return values;
}

static void WriteEnvironmentOverrides(string path, IReadOnlyDictionary<string, string> updates)
{
    List<string> lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
    foreach ((string key, string value) in updates)
    {
        int index = lines.FindIndex(line => line.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
        string serialized = $"{key}={value.Replace("\r", string.Empty).Replace("\n", string.Empty)}";
        if (index >= 0) lines[index] = serialized;
        else { if (lines.Count > 0 && lines[^1].Length > 0) lines.Add(string.Empty); lines.Add(serialized); }
    }
    File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
}

static bool IsValidEnvironmentKey(string key)
    => key.Length > 0 && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

static bool IsSecret(string key)
    => new[] { "PASSWORD", "TOKEN", "SECRET", "CONNECTION_STRING", "ACCESS_KEY", "PRIVATE_KEY" }
        .Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase));

static string Mask(string value) => string.IsNullOrEmpty(value) ? string.Empty : "••••••••";

static async Task RunAsync(string root, RunState state)
{
    try
    {
        List<string> arguments = [
            "run", "--project", Path.Combine(root, "runner", "AutoTest.Runner", "AutoTest.Runner.csproj"),
            "-c", "Release", "--no-build", "--", "--project", state.Project,
        ];
        arguments.AddRange(["--case-ids", string.Join(',', state.CaseIds)]);
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
        state.Attach(process);
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

sealed record RunRequest(string Project, string[]? CaseIds);
sealed record TestItem(string Id, string Name, string[] Tags, bool Destructive, string Group, int StepCount);
sealed record EnvironmentUpdate(Dictionary<string, string> Values);
sealed record GenerateRequest(string SourcePath);
sealed record MessageScanRequest(string SourcePath);
sealed record EnvironmentDefinition(string Key, string Group, string Scope, string DefaultValue);
sealed record EnvironmentGroup(string Name, EnvironmentVariable[] Variables);
sealed record EnvironmentVariable(string Key, string Value, string Source, bool Secret, bool Configured);

sealed class RunState(string id, string project, string[] caseIds, DateTimeOffset startedAt, string historyDirectory)
{
    private readonly object gate = new();
    private readonly List<string> lines = [];
    public string Id { get; } = id;
    public string Project { get; } = project;
    private Process? process;
    public string[] CaseIds { get; } = caseIds;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public string Status { get; private set; } = "running";
    public int? ExitCode { get; private set; }
    public string? ReportPath { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string FailureType { get; private set; } = string.Empty;

    public void Attach(Process value) { lock (gate) process = value; }

    public bool Cancel()
    {
        lock (gate)
        {
            if (Status != "running" || process is null || process.HasExited) return false;
            process.Kill(true);
            Status = "cancelled";
            FinishedAt = DateTimeOffset.Now;
            FailureType = "CANCELLED";
            lines.Add("Workload đã được người dùng dừng.");
            Save();
            return true;
        }
    }

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
        lock (gate)
        {
            if (Status == "cancelled") return;
            ExitCode = exitCode;
            FinishedAt = DateTimeOffset.Now;
            Status = exitCode == 0 ? "passed" : "failed";
            FailureType = exitCode == 0 ? string.Empty : UiSupport.Classify(lines);
            Save();
        }
    }

    public void Fail(string message)
    {
        lock (gate) { lines.Add(message); ExitCode = -1; FinishedAt = DateTimeOffset.Now; Status = "failed"; FailureType = UiSupport.Classify(lines); Save(); }
    }

    public object Snapshot()
    {
        lock (gate)
            return CreateSnapshot();
    }

    private RunSnapshot CreateSnapshot() => new(Id, Project, CaseIds, StartedAt, FinishedAt, Status, ExitCode, ReportPath, FailureType, lines.ToArray());
    private void Save() => UiSupport.SaveHistory(historyDirectory, CreateSnapshot());
}

