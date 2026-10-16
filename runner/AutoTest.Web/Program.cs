using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text;
using System.Security.Cryptography;
using AutoTest.MessageScanner;

// Backend UI: khai báo endpoint, khởi chạy runner nền và trả artifact cho trình duyệt.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseUrls(builder.Configuration["url"] ?? "http://127.0.0.1:5080");
WebApplication app = builder.Build();
string root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
string historyDirectory = Path.Combine(root, ".artifacts", "ui-runs");
Directory.CreateDirectory(historyDirectory);
ConcurrentDictionary<string, RunState> runs = new(StringComparer.OrdinalIgnoreCase);
ConcurrentDictionary<string, WorkbookUploadState> workbookUploads = new(StringComparer.OrdinalIgnoreCase);

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
        .OrderBy(test => test.Group, StringComparer.OrdinalIgnoreCase)
        .ThenBy(test => TestcaseOrdinal(test.Name))
        .ThenBy(test => test.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
});

app.MapPost("/api/projects/{project}/workbooks/analyze", async (string project, HttpRequest request, CancellationToken cancellationToken) =>
{
    _ = SafeProjectDirectory(root, project);
    if (!request.HasFormContentType) return Results.BadRequest(new { code = "TQC-XLSX-001", message = "A multipart XLSX upload is required." });
    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    IFormFile? file = form.Files.GetFile("workbook");
    if (file is null || file.Length == 0) return Results.BadRequest(new { code = "TQC-XLSX-001", message = "The workbook file is missing." });
    if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { code = "TQC-XLSX-002", message = "Only .xlsx workbooks are supported." });

    string uploadId = Guid.NewGuid().ToString("N");
    string directory = Path.Combine(root, ".artifacts", "workbook-imports", project, uploadId);
    Directory.CreateDirectory(directory);
    string workbookPath = Path.Combine(directory, "source.xlsx");
    try
    {
        await using (FileStream output = File.Create(workbookPath)) await file.CopyToAsync(output, cancellationToken);
        ProcessResult result = await RunGeneratorAsync(root, ["--workbook", workbookPath, "--project", project, "--analyze-workbook"]);
        if (result.ExitCode != 0)
        {
            Directory.Delete(directory, true);
            return Results.BadRequest(new { code = "TQC-XLSX-003", message = "Workbook analysis failed.", detail = CleanGeneratorError(result.StandardError) });
        }
        if (!TryExtractJson(result.StandardOutput, out string analysisJson, out string analysisError))
        {
            Directory.Delete(directory, true);
            return Results.BadRequest(new { code = "TQC-XLSX-004", message = "Workbook analysis returned invalid JSON.", detail = analysisError });
        }
        workbookUploads[uploadId] = new(uploadId, project, workbookPath, directory, file.FileName, DateTimeOffset.Now);
        using JsonDocument analysis = JsonDocument.Parse(analysisJson);
        return Results.Ok(new { uploadId, originalFileName = Path.GetFileName(file.FileName), result = analysis.RootElement.Clone() });
    }
    catch
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        throw;
    }
});

app.MapPost("/api/projects/{project}/workbooks/generate", async (string project, WorkbookGenerateRequest request, CancellationToken cancellationToken) =>
{
    _ = SafeProjectDirectory(root, project);
    if (!workbookUploads.TryRemove(request.UploadId, out WorkbookUploadState? upload) || !upload.Project.Equals(project, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { code = "TQC-XLSX-005", message = "The workbook upload is missing or expired." });
    try
    {
        if (request.CaseIds is not { Length: > 0 })
            return Results.BadRequest(new { code = "TQC-XLSX-006", message = "Select at least one supported workbook case." });
        string reviewId = $"workbook-{DateTime.Now:yyyyMMdd-HHmmss}";
        string output = Path.Combine(root, ".artifacts", "generated-testcases", project, reviewId);
        ProcessResult result = await RunGeneratorAsync(root,
        [
            "--workbook", upload.WorkbookPath, "--project", project, "--case-ids", string.Join(',', request.CaseIds),
            "--repository-root", root, "--output", output,
        ]);
        if (result.ExitCode != 0)
            return Results.BadRequest(new { code = "TQC-XLSX-007", message = "Workbook testcase generation failed.", detail = CleanGeneratorError(result.StandardError) });
        if (!TryExtractJson(result.StandardOutput, out string batchJson, out string batchError))
            return Results.BadRequest(new { code = "TQC-XLSX-008", message = "Workbook batch generation returned invalid JSON.", detail = batchError });
        using JsonDocument batch = JsonDocument.Parse(batchJson);
        return Results.Ok(new { reviewId, output, group = "workbook-generated/verify", result = batch.RootElement.Clone() });
    }
    finally
    {
        if (Directory.Exists(upload.Directory)) Directory.Delete(upload.Directory, true);
    }
});
app.MapGet("/api/archify/status", async (string? provider) =>
{
    string archifyCli = ArchifyCliPath(root);
    ProcessResult result = await RunGeneratorAsync(root, ["--ai-status", "--provider", provider ?? "codex-cli"]);
    JsonElement? ai = result.ExitCode == 0 ? JsonDocument.Parse(result.StandardOutput).RootElement.Clone() : null;
    return Results.Ok(new
    {
        available = File.Exists(archifyCli) && result.ExitCode == 0,
        archifyInstalled = File.Exists(archifyCli),
        ai,
    });
});

app.MapGet("/api/archify/guides", async () =>
{
    string archifyCli = ArchifyCliPath(root);
    if (!File.Exists(archifyCli))
        return Results.NotFound(new { code = "TQC-ARCH-003", message = "Archify is not installed." });
    ProcessResult result = await RunProcessAsync("node", [archifyCli, "guide", "--json"], root);
    if (result.ExitCode != 0)
        return Results.BadRequest(new { code = "TQC-ARCH-006", message = "Archify guides could not be loaded.", detail = CleanGeneratorError(result.StandardError) });
    return Results.Text(result.StandardOutput, "application/json; charset=utf-8");
});

app.MapPost("/api/archify/generate", async (ArchifyRequest request) =>
{
    string diagramType = NormalizeDiagramType(request.DiagramType);
    string provider = string.IsNullOrWhiteSpace(request.Provider) ? "codex-cli" : request.Provider;
    string sourcePath = string.IsNullOrWhiteSpace(request.SourcePath) ? root : Path.GetFullPath(request.SourcePath);
    if (!Directory.Exists(sourcePath))
        return Results.BadRequest(new { code = "TQC-ARCH-001", message = "The source directory does not exist.", detail = sourcePath });
    if (string.IsNullOrWhiteSpace(request.Description))
        return Results.BadRequest(new { code = "TQC-ARCH-002", message = "A project description or diagram request is required." });
    string archifyCli = ArchifyCliPath(root);
    if (!File.Exists(archifyCli))
        return Results.BadRequest(new { code = "TQC-ARCH-003", message = "Archify is not installed.", detail = "Install the Archify skill under .agents/skills/archify." });

    string runId = $"archify-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..39];
    string outputDirectory = Path.Combine(root, ".artifacts", "archify", runId);
    Directory.CreateDirectory(outputDirectory);
    string specificationPath = Path.Combine(outputDirectory, $"diagram.{diagramType}.json");
    string semanticPath = Path.Combine(outputDirectory, $"analysis.semantic.{diagramType}.json");
    string htmlPath = Path.Combine(outputDirectory, "diagram.html");
    string requestPath = Path.Combine(outputDirectory, "request.txt");
    await File.WriteAllTextAsync(requestPath, request.Description, new UTF8Encoding(false));
    ProcessResult generation = await RunGeneratorAsync(root,
    [
        "--archify-spec", "--source", sourcePath, "--project", "diagram",
        "--diagram-type", diagramType, "--description-file", requestPath,
        "--visual-preset", request.VisualPreset ?? "classic",
        "--animation", request.Animation ?? "none",
        "--archify-root", Path.Combine(root, ".agents", "skills", "archify"),
        "--provider", provider, "--output", specificationPath,
    ]);
    if (generation.ExitCode != 0)
        return Results.BadRequest(new { code = "TQC-ARCH-AI-001", message = "AI could not create the diagram specification.", detail = CleanGeneratorError(generation.StandardError) });
    File.Copy(specificationPath, semanticPath, overwrite: true);
    DiagramSemantics semantics = CaptureDiagramSemantics(specificationPath, diagramType);

    const int maximumRepairs = 12;
    List<object> repairHistory = [];
    ProcessResult delivery = new(-1, string.Empty, string.Empty);
    for (int repairCount = 0; repairCount <= maximumRepairs; repairCount++)
    {
        delivery = await RunProcessAsync("node", [archifyCli, "deliver", diagramType, specificationPath, htmlPath, "--quality", "showcase", "--json"], root);
        if (delivery.ExitCode == 0) break;
        string diagnosticText = ArchifyDiagnosticText(delivery);
        repairHistory.Add(new { check = repairCount + 1, issues = ArchifyDiagnosticLines(diagnosticText) });
        if (repairCount == maximumRepairs) break;
        if (TryApplyArchifyWorkflowRouteRepair(specificationPath, diagnosticText))
            continue;
        if (TryApplyArchifyReadabilityRepair(specificationPath, diagnosticText))
            continue;
        string diagnosticsPath = Path.Combine(outputDirectory, $"diagnostics-{repairCount + 1:00}.json");
        string repairedPath = Path.Combine(outputDirectory, $"repaired-{repairCount + 1:00}.{diagramType}.json");
        await File.WriteAllTextAsync(diagnosticsPath, diagnosticText, new UTF8Encoding(false));
        ProcessResult repair = await RunGeneratorAsync(root,
        [
            "--archify-repair", "--source", sourcePath, "--diagram-type", diagramType,
            "--specification", specificationPath, "--diagnostics", diagnosticsPath,
            "--archify-root", Path.Combine(root, ".agents", "skills", "archify"),
            "--provider", provider, "--output", repairedPath,
        ]);
        if (repair.ExitCode != 0)
            return Results.BadRequest(new { code = "TQC-ARCH-AI-002", message = "AI could not repair the Archify specification.", detail = CleanGeneratorError(repair.StandardError), specificationPath, repairHistory });
        if (!PreservesDiagramSemantics(repairedPath, diagramType, semantics, out string semanticError))
        {
            repairHistory.Add(new { check = repairHistory.Count + 1, issues = new[] { semanticError } });
            continue;
        }
        File.Copy(repairedPath, specificationPath, overwrite: true);
    }
    if (delivery.ExitCode != 0)
    {
        ProcessResult standardDelivery = await RunProcessAsync("node", [archifyCli, "deliver", diagramType, specificationPath, htmlPath, "--quality", "standard", "--json"], root);
        delivery = standardDelivery;
        if (standardDelivery.ExitCode == 0)
        {
            repairHistory.Add(new { check = repairHistory.Count + 1, issues = new[] { "Tool-QC preserved every semantic item and delivered with the standard composition profile." } });
        }
        if (delivery.ExitCode != 0)
        {
            string diagnostics = ArchifyDiagnosticText(delivery);
            return Results.BadRequest(new
            {
                code = "TQC-ARCH-005",
                message = "Archify validation or rendering failed after automatic repair.",
                detail = string.Join(Environment.NewLine, ArchifyDiagnosticLines(diagnostics).Take(12)),
                specificationPath,
                semanticPath,
                semanticCoverage = "100%",
                repairAttempts = maximumRepairs,
                repairHistory,
            });
        }
    }
    return Results.Ok(new
    {
        runId,
        diagramType,
        specificationPath,
        semanticPath,
        semanticCoverage = "100%",
        htmlPath,
        url = $"/api/archify/{runId}",
        repairAttempts = repairHistory.Count,
        message = repairHistory.Count == 0 ? "The architecture diagram was generated and validated successfully." : $"The architecture diagram was repaired and validated after {repairHistory.Count} round(s).",
    });
});

app.MapGet("/api/archify/{runId}", (string runId) =>
{
    string safeRunId = SafeName(runId, string.Empty);
    if (!safeRunId.Equals(runId, StringComparison.Ordinal)) return Results.BadRequest();
    string file = Path.GetFullPath(Path.Combine(root, ".artifacts", "archify", safeRunId, "diagram.html"));
    string artifactRoot = Path.GetFullPath(Path.Combine(root, ".artifacts", "archify")) + Path.DirectorySeparatorChar;
    if (!file.StartsWith(artifactRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) return Results.NotFound();
    return Results.File(file, "text/html; charset=utf-8");
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

app.MapGet("/api/generator/status", async (string? provider) =>
{
    string selectedProvider = string.IsNullOrWhiteSpace(provider) ? "codex-cli" : provider;
    ProcessResult result = await RunGeneratorAsync(root, ["--ai-status", "--provider", selectedProvider]);
    if (result.ExitCode != 0)
        return Results.Ok(new { provider = selectedProvider, available = false, authenticated = false, message = result.StandardError });
    if (!TryExtractJson(result.StandardOutput, out string statusJson, out string statusError))
        return Results.Ok(new { provider = selectedProvider, available = false, authenticated = false, message = statusError });
    return Results.Text(statusJson, "application/json");
});

app.MapPost("/api/projects/{project}/generate", async (string project, GenerateRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { message = "Đường dẫn source không tồn tại." });
    string output = Path.Combine(root, ".artifacts", "generated-testcases", project, $"source-draft-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    ProcessStartInfo info = Utf8Process("dotnet", root);
    foreach (string argument in new[] { "run", "--project", Path.Combine(root, "runner", "AutoTest.Generator", "AutoTest.Generator.csproj"), "-c", "Release", "--", "--source", request.SourcePath, "--project", project, "--output", output }) info.ArgumentList.Add(argument);
    using Process process = new() { StartInfo = info }; process.Start();
    string standardOutput = await process.StandardOutput.ReadToEndAsync(); string standardError = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
    return process.ExitCode == 0 ? Results.Ok(new { output, message = standardOutput.Trim() }) : Results.BadRequest(new { message = standardError.Trim() });
});

app.MapPost("/api/projects/{project}/generate/analyze", async (string project, GenerateRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { code = "TQC-GEN-001", message = "The source directory does not exist." });
    List<string> arguments = ["--source", request.SourcePath, "--project", project, "--analyze", "--mode", request.Mode ?? "local", "--provider", request.Provider ?? "codex-cli"];
    if (!string.IsNullOrWhiteSpace(request.Module)) arguments.AddRange(["--module", request.Module]);
    ProcessResult result = await RunGeneratorAsync(root, arguments);
    if (result.ExitCode != 0)
    {
        string detail = CleanGeneratorError(result.StandardError);
        if (detail.Contains("Codex CLI exceeded", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new
            {
                code = "TQC-GEN-AI-004",
                message = "AI source analysis timed out.",
                detail = "Source quá lớn để AI phân tích toàn bộ trong một lượt. Chọn Phạm vi phân tích = Theo module, chọn một module rồi chạy lại; hoặc chọn chế độ Chuyên sâu — ưu tiên AI để tự quay về Roslyn khi AI quá thời gian.",
            });
        return Results.BadRequest(new { code = "TQC-GEN-002", message = "Source analysis failed.", detail });
    }
    if (!TryExtractJson(result.StandardOutput, out string analysisJson, out string analysisError))
        return Results.BadRequest(new { code = "TQC-GEN-007", message = "Source analysis returned invalid JSON.", detail = analysisError });
    return Results.Text(analysisJson, "application/json");
});

app.MapPost("/api/projects/{project}/generate/modules", async (string project, GenerateRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { code = "TQC-GEN-001", message = "The source directory does not exist." });
    ProcessResult result = await RunGeneratorAsync(root, ["--source", request.SourcePath, "--project", project, "--list-modules"]);
    if (result.ExitCode != 0) return Results.BadRequest(new { code = "TQC-GEN-010", message = "Source module discovery failed.", detail = result.StandardError });
    if (!TryExtractJson(result.StandardOutput, out string modulesJson, out string modulesError)) return Results.BadRequest(new { code = "TQC-GEN-011", message = "Module discovery returned invalid JSON.", detail = modulesError });
    return Results.Text(modulesJson, "application/json");
});

app.MapPost("/api/projects/{project}/generate/draft", async (string project, GenerateDraftRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { code = "TQC-GEN-001", message = "The source directory does not exist." });
    string output = Path.Combine(root, ".artifacts", "generated-testcases", project, $"source-draft-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    List<string> arguments = ["--source", request.SourcePath, "--project", project, "--output", output, "--mode", request.Mode ?? "auto", "--provider", request.Provider ?? "codex-cli"];
    if (request.EndpointIds is { Length: > 0 }) arguments.AddRange(["--include", string.Join(',', request.EndpointIds)]);
    ProcessResult result = await RunGeneratorAsync(root, arguments);
    string reviewId = Path.GetFileNameWithoutExtension(output);
    GeneratorMetadata metadata = await ReadGeneratorMetadataAsync(output + ".meta.json");
    return result.ExitCode == 0
        ? Results.Ok(new { reviewId, output, json = await File.ReadAllTextAsync(output), message = metadata.Message, provider = metadata.Provider, aiUsed = metadata.AiUsed, fellBack = metadata.FellBack })
        : Results.BadRequest(new { code = "TQC-GEN-003", message = "Testcase draft generation failed.", detail = result.StandardError });
});

app.MapPost("/api/projects/{project}/generate/preflight", async (string project, DraftContentRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (!TryNormalizeDraft(request.Json, project, activate: false, out string normalized, out string error))
        return Results.BadRequest(new { code = "TQC-GEN-004", message = "The draft JSON is invalid.", detail = error });
    string reviewId = SafeName(request.ReviewId, "review");
    string directory = Path.Combine(root, ".artifacts", "generated-testcases", project, "preflight", reviewId);
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "suite.json"), normalized);
    ProcessResult result = await RunRunnerPreflightAsync(root, project, directory, requireCompleteCoverage: false);
    ProcessResult officialCheck = result.ExitCode == 0
        ? await RunRunnerPreflightAsync(root, project, directory, requireCompleteCoverage: true)
        : result;
    return result.ExitCode == 0
        ? Results.Ok(new
        {
            passed = true,
            readyToSave = officialCheck.ExitCode == 0,
            message = result.StandardOutput,
            warnings = officialCheck.ExitCode == 0 ? Array.Empty<string>() : PreflightLines(officialCheck),
        })
        : Results.BadRequest(new { code = "TQC-GEN-005", message = "Testcase preflight failed.", detail = string.Join(Environment.NewLine, new[] { result.StandardError, result.StandardOutput }.Where(value => !string.IsNullOrWhiteSpace(value))) });
});

app.MapPost("/api/projects/{project}/generate/repair", async (string project, RepairDraftRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        return Results.BadRequest(new { code = "TQC-GEN-001", message = "The source directory does not exist." });
    string reviewId = SafeName(request.ReviewId, "review");
    string directory = Path.Combine(root, ".artifacts", "generated-testcases", project, "repair", reviewId);
    Directory.CreateDirectory(directory);
    string currentJson = request.Json;
    int maximumRepairs = Math.Clamp(request.MaxAttempts, 1, 10);
    List<object> attempts = [];
    for (int repairCount = 0; repairCount <= maximumRepairs; repairCount++)
    {
        if (!TryNormalizeDraft(currentJson, project, activate: false, out string normalized, out string normalizeError))
            return Results.BadRequest(new { code = "TQC-GEN-004", message = "The draft JSON is invalid.", detail = normalizeError });
        string attemptDirectory = Path.Combine(directory, $"check-{repairCount + 1:00}");
        Directory.CreateDirectory(attemptDirectory);
        string suitePath = Path.Combine(attemptDirectory, "suite.json");
        await File.WriteAllTextAsync(suitePath, normalized);
        ProcessResult preflight = await RunRunnerPreflightAsync(root, project, attemptDirectory, requireCompleteCoverage: false);
        if (preflight.ExitCode == 0)
        {
            ProcessResult official = await RunRunnerPreflightAsync(root, project, attemptDirectory, requireCompleteCoverage: true);
            return Results.Ok(new
            {
                repaired = repairCount > 0,
                attempts = repairCount,
                json = normalized,
                readyToSave = official.ExitCode == 0,
                warnings = official.ExitCode == 0 ? Array.Empty<string>() : PreflightLines(official),
                history = attempts,
                message = $"Testcase passed preflight after {repairCount} repair round(s).",
            });
        }
        string[] issues = PreflightLines(preflight);
        attempts.Add(new { check = repairCount + 1, repairCount, issues });
        if (repairCount == maximumRepairs)
            break;
        string issuesPath = Path.Combine(attemptDirectory, "issues.json");
        string repairedPath = Path.Combine(attemptDirectory, "repaired.json");
        await File.WriteAllTextAsync(issuesPath, JsonSerializer.Serialize(issues, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        ProcessResult repair = await RunGeneratorAsync(root,
        [
            "--repair-draft", "--source", request.SourcePath, "--project", project,
            "--draft", suitePath, "--issues", issuesPath, "--output", repairedPath,
            "--provider", request.Provider ?? "codex-cli",
        ]);
        if (repair.ExitCode != 0)
            return Results.BadRequest(new { code = "TQC-GEN-AI-006", message = "AI testcase repair failed.", detail = CleanGeneratorError(repair.StandardError), attempts = repairCount + 1, history = attempts });
        currentJson = await File.ReadAllTextAsync(repairedPath);
    }
    return Results.BadRequest(new
    {
        code = "TQC-GEN-012",
        message = "Automatic testcase repair reached the attempt limit.",
        detail = $"AI đã sửa và kiểm tra lại {maximumRepairs} vòng nhưng preflight vẫn còn lỗi. JSON cuối được giữ lại để kiểm tra thủ công.",
        json = currentJson,
        attempts = maximumRepairs,
        history = attempts,
    });
});

app.MapPost("/api/projects/{project}/generate/activate", async (string project, ActivateDraftRequest request) =>
{
    string projectDirectory = SafeProjectDirectory(root, project);
    if (!TryNormalizeDraft(request.Json, project, activate: true, out string normalized, out string error))
        return Results.BadRequest(new { code = "TQC-GEN-004", message = "The draft JSON is invalid.", detail = error });
    string reviewId = SafeName(request.ReviewId, "review");
    string preflightDirectory = Path.Combine(root, ".artifacts", "generated-testcases", project, "preflight", reviewId);
    Directory.CreateDirectory(preflightDirectory);
    await File.WriteAllTextAsync(Path.Combine(preflightDirectory, "suite.json"), normalized);
    ProcessResult result = await RunRunnerPreflightAsync(root, project, preflightDirectory, requireCompleteCoverage: true);
    if (result.ExitCode != 0) return Results.BadRequest(new { code = "TQC-GEN-005", message = "Testcase preflight failed.", detail = result.StandardError });
    string group = SafeRelativeGroup(request.Group, "generated");
    string fileName = SafeName(request.FileName, $"source-{DateTime.Now:yyyyMMdd-HHmmss}") + ".json";
    string output = Path.Combine(projectDirectory, "testcases", group, fileName);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    if (File.Exists(output))
    {
        string currentHash = await Sha256Async(output);
        string currentContent = await File.ReadAllTextAsync(output);
        if (currentContent.Equals(normalized, StringComparison.Ordinal))
            return Results.Ok(new { output, status = "unchanged", sha256 = currentHash, message = "The generated testcase is already up to date." });
        if (!request.ConfirmOverwrite)
            return Results.Conflict(new { code = "TQC-GEN-013", status = "update", message = "A generated testcase already exists. Review the diff and confirm overwrite.", output, currentHash });
        if (!string.IsNullOrWhiteSpace(request.ExpectedSha256) && !currentHash.Equals(request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { code = "TQC-GEN-014", status = "conflict", message = "The target changed after preview. Reload before overwriting.", output, currentHash });
    }
    await File.WriteAllTextAsync(output, normalized);
    return Results.Ok(new { output, status = "saved", sha256 = await Sha256Async(output), message = "The testcase suite passed preflight and was activated." });
});

app.MapPost("/api/projects/{project}/generate/run", async (string project, DraftContentRequest request) =>
{
    _ = SafeProjectDirectory(root, project);
    if (!TryNormalizeDraft(request.Json, project, activate: true, out string normalized, out string error))
        return Results.BadRequest(new { code = "TQC-GEN-004", message = "The draft JSON is invalid.", detail = error });
    string reviewId = SafeName(request.ReviewId, "review");
    string runId = Guid.NewGuid().ToString("N");
    string directory = Path.Combine(root, ".artifacts", "generated-testcases", project, "runs", reviewId, runId);
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "suite.json"), normalized);
    ProcessResult preflight = await RunRunnerPreflightAsync(root, project, directory, requireCompleteCoverage: false);
    if (preflight.ExitCode != 0)
        return Results.BadRequest(new { code = "TQC-GEN-005", message = "Testcase preflight failed.", detail = string.Join(Environment.NewLine, new[] { preflight.StandardError, preflight.StandardOutput }.Where(value => !string.IsNullOrWhiteSpace(value))) });
    using JsonDocument document = JsonDocument.Parse(normalized);
    string[] caseIds = document.RootElement.GetProperty("cases").EnumerateArray()
        .Select(testCase => testCase.GetProperty("id").GetString())
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id!)
        .ToArray();
    RunState state = new(runId, project, caseIds, DateTimeOffset.Now, historyDirectory, directory, request.SequentialChain);
    if (caseIds.Length > 1 && !request.SequentialChain)
        return Results.BadRequest(new { code = "TQC-RUN-021", message = "Multiple generated testcases require sequential chain mode.", detail = "Enable 'Sinh chuỗi theo thứ tự chọn' so saved response values and authentication tokens are passed to the next API." });
    runs[runId] = state;
    _ = Task.Run(() => RunAsync(root, state));
    return Results.Accepted($"/api/runs/{runId}", new { runId });
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
    RunState state = new(runId, request.Project, request.CaseIds, DateTimeOffset.Now, historyDirectory, sequentialChain: request.SequentialChain);
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

static string ArchifyCliPath(string root) => Path.Combine(root, ".agents", "skills", "archify", "bin", "archify.mjs");
static string NormalizeDiagramType(string? value) => value?.Trim().ToLowerInvariant() switch
{
    "workflow" => "workflow",
    "sequence" => "sequence",
    "dataflow" => "dataflow",
    "lifecycle" => "lifecycle",
    _ => "architecture",
};
static async Task<ProcessResult> RunProcessAsync(string fileName, IEnumerable<string> arguments, string workingDirectory)
{
    ProcessStartInfo info = Utf8Process(fileName, workingDirectory);
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    using Process process = new() { StartInfo = info };
    process.Start();
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new(process.ExitCode, (await output).Trim(), (await error).Trim());
}
static string ArchifyDiagnosticText(ProcessResult result)
    => string.Join(Environment.NewLine, new[] { result.StandardOutput, result.StandardError }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
static string[] ArchifyDiagnosticLines(string value)
{
    try
    {
        using JsonDocument document = JsonDocument.Parse(value);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("diagnostics", out JsonElement diagnostics) && diagnostics.ValueKind == JsonValueKind.Array)
            return diagnostics.EnumerateArray().Select(item =>
            {
                string code = item.TryGetProperty("code", out JsonElement codeElement) ? codeElement.GetString() ?? "archify" : "archify";
                string message = item.TryGetProperty("message", out JsonElement messageElement) ? messageElement.GetString() ?? "Unknown validation error." : "Unknown validation error.";
                return $"{code}: {message}";
            }).ToArray();
        if (root.TryGetProperty("error", out JsonElement error)) return [error.GetString() ?? "Archify validation failed."];
    }
    catch { }
    return value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(line => line is not "{" and not "}").Take(12).ToArray();
}
static DiagramSemantics CaptureDiagramSemantics(string specificationPath, string diagramType)
{
    JsonObject specification = JsonNode.Parse(File.ReadAllText(specificationPath)) as JsonObject
        ?? throw new InvalidOperationException("Archify specification is not a JSON object.");
    (string nodeCollection, string relationCollection, string fromProperty, string toProperty) = DiagramSemanticCollections(diagramType);
    string[] nodeIds = (specification[nodeCollection] as JsonArray ?? []).OfType<JsonObject>()
        .Select(node => node["id"]?.GetValue<string>()).OfType<string>().Order(StringComparer.Ordinal).ToArray();
    string[] relations = (specification[relationCollection] as JsonArray ?? []).OfType<JsonObject>()
        .Select(relation => $"{relation[fromProperty]?.GetValue<string>()}->{relation[toProperty]?.GetValue<string>()}")
        .Order(StringComparer.Ordinal).ToArray();
    return new(nodeCollection, relationCollection, nodeIds, relations);
}
static bool PreservesDiagramSemantics(string specificationPath, string diagramType, DiagramSemantics expected, out string error)
{
    try
    {
        DiagramSemantics actual = CaptureDiagramSemantics(specificationPath, diagramType);
        string[] missingNodes = expected.NodeIds.Except(actual.NodeIds, StringComparer.Ordinal).ToArray();
        string[] missingRelations = expected.Relations.Except(actual.Relations, StringComparer.Ordinal).ToArray();
        if (missingNodes.Length == 0 && missingRelations.Length == 0)
        {
            error = string.Empty;
            return true;
        }
        error = $"AI repair was rejected because it removed semantic content. Missing nodes: {string.Join(", ", missingNodes.Take(8))}. Missing relationships: {string.Join(", ", missingRelations.Take(8))}.";
        return false;
    }
    catch (Exception exception)
    {
        error = $"AI repair was rejected because semantic preservation could not be verified: {exception.Message}";
        return false;
    }
}
static (string Nodes, string Relations, string From, string To) DiagramSemanticCollections(string diagramType) => diagramType switch
{
    "architecture" => ("components", "connections", "from", "to"),
    "workflow" => ("nodes", "edges", "from", "to"),
    "sequence" => ("participants", "messages", "from", "to"),
    "dataflow" => ("nodes", "flows", "from", "to"),
    "lifecycle" => ("states", "transitions", "from", "to"),
    _ => throw new InvalidOperationException($"Unsupported diagram type '{diagramType}'."),
};
static bool TryApplyArchifyReadabilityRepair(string specificationPath, string diagnostics)
{
    try
    {
        using JsonDocument receipt = JsonDocument.Parse(diagnostics);
        if (!receipt.RootElement.TryGetProperty("diagnostics", out JsonElement items)
            || !items.EnumerateArray().Any(item => item.TryGetProperty("code", out JsonElement code)
                && code.GetString() == "composition/desktop-readability"))
            return false;
        JsonNode? specification = JsonNode.Parse(File.ReadAllText(specificationPath));
        JsonArray? components = specification?["components"] as JsonArray;
        if (components is null) return false;
        bool changed = false;
        foreach (JsonObject component in components.OfType<JsonObject>())
        {
            string? sublabel = component["sublabel"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(sublabel))
            {
                component.Remove("sublabel");
                changed = true;
            }
        }
        if (!changed) return false;
        File.WriteAllText(specificationPath, specification!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return true;
    }
    catch { return false; }
}
static bool TryApplyArchifyWorkflowRouteRepair(string specificationPath, string diagnostics)
{
    try
    {
        using JsonDocument receipt = JsonDocument.Parse(diagnostics);
        if (!receipt.RootElement.TryGetProperty("diagnostics", out JsonElement diagnosticItems)) return false;
        HashSet<string> edgeIds = diagnosticItems.EnumerateArray()
            .Where(item => item.TryGetProperty("code", out JsonElement code) && code.GetString() == "workflow/route-preset-conflict")
            .Select(item => item.TryGetProperty("subject", out JsonElement subject)
                && subject.TryGetProperty("id", out JsonElement id) ? id.GetString() : null)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (edgeIds.Count == 0) return false;
        JsonObject? specification = JsonNode.Parse(File.ReadAllText(specificationPath)) as JsonObject;
        JsonArray? edges = specification?["edges"] as JsonArray;
        if (edges is null) return false;
        bool changed = false;
        foreach (JsonObject edge in edges.OfType<JsonObject>())
        {
            string? id = edge["id"]?.GetValue<string>();
            if (id is null || !edgeIds.Contains(id)) continue;
            edge["route"] = "auto";
            edge.Remove("via");
            edge.Remove("fromSide");
            edge.Remove("toSide");
            changed = true;
        }
        if (!changed) return false;
        File.WriteAllText(specificationPath, specification!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return true;
    }
    catch { return false; }
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

static int TestcaseOrdinal(string name)
{
    const string prefix = "Testcase ";
    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return int.MaxValue;
    int separator = name.IndexOf(':', prefix.Length);
    if (separator <= prefix.Length) return int.MaxValue;
    return int.TryParse(name.AsSpan(prefix.Length, separator - prefix.Length), out int ordinal)
        ? ordinal
        : int.MaxValue;
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

// Chạy generator trong process nền để UI không phụ thuộc trực tiếp vào implementation Roslyn.
static async Task<ProcessResult> RunGeneratorAsync(string root, IEnumerable<string> generatorArguments)
{
    string generatorDll = Path.Combine(root, "runner", "AutoTest.Generator", "bin", "Release", "net8.0", "AutoTest.Generator.dll");
    if (!File.Exists(generatorDll))
        return new(127, string.Empty, "TQC-GEN-008: Generator has not been built. Restart Tool-QC with npm start.");
    ProcessStartInfo info = Utf8Process("dotnet", root);
    foreach (string argument in new[] { generatorDll }.Concat(generatorArguments)) info.ArgumentList.Add(argument);
    using Process process = new() { StartInfo = info };
    process.Start();
    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new(process.ExitCode, output.Trim(), error.Trim());
}

static async Task<GeneratorMetadata> ReadGeneratorMetadataAsync(string path)
{
    if (!File.Exists(path)) return new("local-roslyn", false, false, "The testcase draft was generated successfully.");
    try
    {
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        JsonElement root = document.RootElement;
        return new(
            root.TryGetProperty("provider", out JsonElement provider) ? provider.GetString() ?? "local-roslyn" : "local-roslyn",
            root.TryGetProperty("aiUsed", out JsonElement aiUsed) && aiUsed.GetBoolean(),
            root.TryGetProperty("fellBack", out JsonElement fellBack) && fellBack.GetBoolean(),
            root.TryGetProperty("message", out JsonElement message) ? message.GetString() ?? "The testcase draft was generated successfully." : "The testcase draft was generated successfully.");
    }
    catch
    {
        return new("local-roslyn", false, false, "The testcase draft was generated successfully.");
    }
}

static async Task<ProcessResult> RunRunnerPreflightAsync(string root, string project, string testcaseDirectory, bool requireCompleteCoverage)
{
    string runnerDll = Path.Combine(root, "runner", "AutoTest.Runner", "bin", "Release", "net8.0", "AutoTest.Runner.dll");
    if (!File.Exists(runnerDll))
        return new(127, string.Empty, "TQC-GEN-009: Test runner has not been built. Restart Tool-QC with npm start.");
    string[] arguments = [runnerDll, "--project", project, "--testcase-directory", testcaseDirectory, "--preflight-only"];
    ProcessStartInfo info = Utf8Process("dotnet", root);
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    if (!requireCompleteCoverage) info.ArgumentList.Add("--allow-incomplete-coverage");
    using Process process = new() { StartInfo = info };
    process.Start();
    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
    try
    {
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(timeout.Token);
        return new(process.ExitCode, (await outputTask).Trim(), (await errorTask).Trim());
    }
    catch (OperationCanceledException)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        return new(124, string.Empty, "TQC-GEN-006: Preflight exceeded 30 seconds and was stopped. Build the runner once, then retry.");
    }
}

static string[] PreflightLines(ProcessResult result) => string.Join(
        Environment.NewLine,
        new[] { result.StandardError, result.StandardOutput }.Where(value => !string.IsNullOrWhiteSpace(value)))
    .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(line => !line.StartsWith("Preflight phát hiện", StringComparison.OrdinalIgnoreCase))
    .Select(line => line.TrimStart('-', ' '))
    .ToArray();

static string CleanGeneratorError(string error)
{
    string[] lines = error.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    string? useful = lines.FirstOrDefault(line => !line.StartsWith("at ", StringComparison.OrdinalIgnoreCase)
        && !line.StartsWith("---", StringComparison.OrdinalIgnoreCase)
        && !line.Contains("Program.<Main>", StringComparison.OrdinalIgnoreCase));
    return useful?.Replace("Unhandled exception. ", string.Empty, StringComparison.OrdinalIgnoreCase)
        ?? "The generator failed without a diagnostic message.";
}

static bool TryNormalizeDraft(string json, string project, bool activate, out string normalized, out string error)
{
    normalized = string.Empty; error = string.Empty;
    try
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        JsonElement rootElement = document.RootElement;
        if (!rootElement.TryGetProperty("project", out JsonElement projectElement) || !string.Equals(projectElement.GetString(), project, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Project in JSON does not match the selected project.");
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("JSON root must be an object.");
        if (activate)
        {
            node.Remove("generatedDraft"); node.Remove("disabled");
            if (node["cases"] is System.Text.Json.Nodes.JsonArray cases) foreach (System.Text.Json.Nodes.JsonNode? item in cases) if (item is System.Text.Json.Nodes.JsonObject testcase) { testcase.Remove("disabled"); testcase.Remove("draft"); testcase.Remove("detectedContract"); }
        }
        normalized = node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return true;
    }
    catch (Exception exception) { error = exception.Message; return false; }
}

// dotnet run có thể ghi thông tin build trước payload; chỉ trả đúng JSON cho trình duyệt.
static bool TryExtractJson(string output, out string json, out string error)
{
    json = string.Empty;
    error = string.Empty;
    int objectStart = output.IndexOf('{');
    int arrayStart = output.IndexOf('[');
    int start = objectStart < 0 ? arrayStart : arrayStart < 0 ? objectStart : Math.Min(objectStart, arrayStart);
    if (start < 0)
    {
        error = string.IsNullOrWhiteSpace(output) ? "Generator did not return output." : output.Trim();
        return false;
    }

    string candidate = output[start..].Trim();
    try
    {
        using JsonDocument document = JsonDocument.Parse(candidate);
        json = document.RootElement.GetRawText();
        return true;
    }
    catch (JsonException exception)
    {
        error = $"Generator output could not be parsed: {exception.Message}";
        return false;
    }
}

static string SafeName(string? value, string fallback)
{
    string cleaned = new string((string.IsNullOrWhiteSpace(value) ? fallback : value).Trim().Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray()).Trim('-');
    return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
}

static string SafeRelativeGroup(string? value, string fallback)
{
    string[] segments = (string.IsNullOrWhiteSpace(value) ? fallback : value)
        .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(segment => SafeName(segment, "group"))
        .Where(segment => segment is not "." and not "..")
        .ToArray();
    return segments.Length == 0 ? fallback : Path.Combine(segments);
}

static async Task<string> Sha256Async(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static async Task RunAsync(string root, RunState state)
{
    try
    {
        string runnerDll = Path.Combine(root, "runner", "AutoTest.Runner", "bin", "Release", "net8.0", "AutoTest.Runner.dll");
        List<string> arguments = [runnerDll, "--project", state.Project];
        if (!string.IsNullOrWhiteSpace(state.TestcaseDirectory))
        {
            arguments.AddRange(["--testcase-directory", state.TestcaseDirectory]);
            arguments.Add("--allow-incomplete-coverage");
        }
        else
            arguments.AddRange(["--case-ids", string.Join(',', state.CaseIds)]);
        if (state.SequentialChain) arguments.Add("--sequential-chain");
        ProcessStartInfo startInfo = Utf8Process("dotnet", root);
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

// Chuẩn hóa UTF-8 cho mọi process con để message tiếng Việt không phụ thuộc code page Windows.
static ProcessStartInfo Utf8Process(string fileName, string workingDirectory) => new(fileName)
{
    WorkingDirectory = workingDirectory,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    StandardOutputEncoding = Encoding.UTF8,
    StandardErrorEncoding = Encoding.UTF8,
    UseShellExecute = false,
    CreateNoWindow = true,
};

sealed record RunRequest(string Project, string[]? CaseIds, bool SequentialChain = false);
sealed record TestItem(string Id, string Name, string[] Tags, bool Destructive, string Group, int StepCount);
sealed record EnvironmentUpdate(Dictionary<string, string> Values);
sealed record ArchifyRequest(string? SourcePath, string Description, string? DiagramType = "architecture", string? Provider = "codex-cli", string? VisualPreset = "classic", string? Animation = "none");
sealed record GenerateRequest(string SourcePath, string? Mode = "local", string? Module = null, string? Provider = "codex-cli");
sealed record GenerateDraftRequest(string SourcePath, string[]? EndpointIds, string? Mode = "auto", string? Provider = "codex-cli");
sealed record DraftContentRequest(string ReviewId, string Json, bool SequentialChain = false);
sealed record RepairDraftRequest(string ReviewId, string Json, string SourcePath, string? Provider = "codex-cli", int MaxAttempts = 5);
sealed record ActivateDraftRequest(string ReviewId, string Json, string? Group, string? FileName, bool ConfirmOverwrite = false, string? ExpectedSha256 = null);
sealed record WorkbookGenerateRequest(string UploadId, string[] CaseIds);
sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
sealed record DiagramSemantics(string NodeCollection, string RelationCollection, string[] NodeIds, string[] Relations);
sealed record MessageScanRequest(string SourcePath);
sealed record EnvironmentDefinition(string Key, string Group, string Scope, string DefaultValue);
sealed record EnvironmentGroup(string Name, EnvironmentVariable[] Variables);
sealed record EnvironmentVariable(string Key, string Value, string Source, bool Secret, bool Configured);
sealed record GeneratorMetadata(string Provider, bool AiUsed, bool FellBack, string Message);
sealed record WorkbookUploadState(string Id, string Project, string WorkbookPath, string Directory, string OriginalFileName, DateTimeOffset CreatedAt);

sealed class RunState(string id, string project, string[] caseIds, DateTimeOffset startedAt, string historyDirectory, string? testcaseDirectory = null, bool sequentialChain = false)
{
    private readonly object gate = new();
    private readonly List<string> lines = [];
    public string Id { get; } = id;
    public string Project { get; } = project;
    private Process? process;
    public string[] CaseIds { get; } = caseIds;
    public bool SequentialChain { get; } = sequentialChain;
    public string? TestcaseDirectory { get; } = testcaseDirectory;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public string Status { get; private set; } = "running";
    public int? ExitCode { get; private set; }
    public string? ReportPath { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string FailureType { get; private set; } = string.Empty;
    public string FailureCategory { get; private set; } = string.Empty;
    public string FailureMessage { get; private set; } = string.Empty;
    public string FixHint { get; private set; } = string.Empty;

    public void Attach(Process value) { lock (gate) process = value; }

    public bool Cancel()
    {
        lock (gate)
        {
            if (Status != "running" || process is null || process.HasExited) return false;
            process.Kill(true);
            Status = "cancelled";
            FinishedAt = DateTimeOffset.Now;
            FailureType = "TQC-RUN-010";
            FailureCategory = "CANCELLED";
            FailureMessage = "The workload was cancelled by the user.";
            FixHint = "Run it again when the environment is ready or reduce the workload scope.";
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
            FailureDiagnosis diagnosis = exitCode == 0 ? new(string.Empty, string.Empty, string.Empty, string.Empty) : UiSupport.Diagnose(lines);
            FailureType = diagnosis.Code;
            FailureCategory = diagnosis.Category;
            FailureMessage = diagnosis.Message;
            FixHint = diagnosis.FixHint;
            Save();
        }
    }

    public void Fail(string message)
    {
        lock (gate) { lines.Add(message); ExitCode = -1; FinishedAt = DateTimeOffset.Now; Status = "failed"; FailureDiagnosis diagnosis = UiSupport.Diagnose(lines); FailureType = diagnosis.Code; FailureCategory = diagnosis.Category; FailureMessage = diagnosis.Message; FixHint = diagnosis.FixHint; Save(); }
    }

    public object Snapshot()
    {
        lock (gate)
            return CreateSnapshot();
    }

    private RunSnapshot CreateSnapshot() => new(Id, Project, CaseIds, StartedAt, FinishedAt, Status, ExitCode, ReportPath, FailureType, FailureCategory, FailureMessage, FixHint, lines.ToArray());
    private void Save() => UiSupport.SaveHistory(historyDirectory, CreateSnapshot());
}


