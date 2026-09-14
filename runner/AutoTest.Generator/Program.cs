// CLI generator: phân tích source bằng Roslyn và sinh testcase nháp không phụ thuộc AI.
using System.Text.Json;
using System.Text;
using AutoTest.Generator;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

string? Argument(string key)
{
    int index = Array.FindIndex(args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("AutoTest.Generator --source <path> --project <name> [--mode auto|ai|local] [--module <name>] [--list-modules] [--analyze] [--include <id,id>] [--output <file.json>]");
    return;
}

if (args.Contains("--ai-status", StringComparer.OrdinalIgnoreCase))
{
    AiProviderStatus status = await HybridDraftGenerator.ProbeAsync(Argument("--provider") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_PROVIDER") ?? "codex-cli");
    Console.WriteLine(JsonSerializer.Serialize(status, GeneratorJson.Options));
    return;
}

if (args.Contains("--repair-draft", StringComparer.OrdinalIgnoreCase))
{
    string repairSource = Path.GetFullPath(Argument("--source") ?? string.Empty);
    string repairProject = Argument("--project") ?? string.Empty;
    string draftPath = Path.GetFullPath(Argument("--draft") ?? string.Empty);
    string issuesPath = Path.GetFullPath(Argument("--issues") ?? string.Empty);
    string repairOutput = Path.GetFullPath(Argument("--output") ?? string.Empty);
    if (!Directory.Exists(repairSource) || string.IsNullOrWhiteSpace(repairProject) || !File.Exists(draftPath) || !File.Exists(issuesPath) || string.IsNullOrWhiteSpace(repairOutput))
        throw new InvalidOperationException("Use --repair-draft --source <path> --project <name> --draft <json> --issues <json> --output <json>.");
    GeneratorExecutionOptions repairOptions = GeneratorExecutionOptions.From(
        "ai",
        Argument("--provider") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_PROVIDER"),
        Argument("--timeout-seconds") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_TIMEOUT_SECONDS"),
        "false");
    string[] repairIssues = JsonSerializer.Deserialize<string[]>(File.ReadAllText(issuesPath), GeneratorJson.Options) ?? [];
    string repaired = await HybridDraftGenerator.RepairDraftAsync(repairProject, repairSource, File.ReadAllText(draftPath), repairIssues, repairOptions);
    Directory.CreateDirectory(Path.GetDirectoryName(repairOutput)!);
    File.WriteAllText(repairOutput, repaired);
    Console.WriteLine(repairOutput);
    return;
}

if (Argument("--coverage-report") is { } coverageReport)
{
    CoverageAnalysis coverage = CoberturaCoverageReader.Read(coverageReport);
    string json = JsonSerializer.Serialize(coverage, GeneratorJson.Options);
    if (Argument("--coverage-output") is { } coverageOutput)
    {
        string path = Path.GetFullPath(coverageOutput); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, json); Console.WriteLine(path);
    }
    else Console.WriteLine(json);
    return;
}

string source = Path.GetFullPath(Argument("--source") ?? string.Empty);
string project = Argument("--project") ?? string.Empty;
if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(project))
    throw new InvalidOperationException("Use --source <source-path> --project <project-name>.");

if (args.Contains("--list-modules", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(JsonSerializer.Serialize(SourceContractAnalyzer.DiscoverModules(source), GeneratorJson.Options));
    return;
}

GeneratorExecutionOptions generatorOptions = GeneratorExecutionOptions.From(
    Argument("--mode") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_MODE"),
    Argument("--provider") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_PROVIDER"),
    Argument("--timeout-seconds") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_TIMEOUT_SECONDS"),
    Argument("--fallback-enabled") ?? Environment.GetEnvironmentVariable("TESTCASE_GENERATOR_FALLBACK_ENABLED"));
GeneratorAnalysis analysis = new SourceContractAnalyzer().Analyze(source, Argument("--module"));
string? analysisOutput = Argument("--analysis-output");
if (args.Contains("--analyze", StringComparer.OrdinalIgnoreCase))
{
    analysis = await HybridDraftGenerator.AnalyzeAsync(project, source, analysis, generatorOptions);
    string json = JsonSerializer.Serialize(analysis, GeneratorJson.Options);
    if (string.IsNullOrWhiteSpace(analysisOutput)) Console.WriteLine(json);
    else
    {
        string path = Path.GetFullPath(analysisOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        Console.WriteLine(path);
    }
    return;
}

string[]? included = Argument("--include")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
string output = Path.GetFullPath(Argument("--output") ?? Path.Combine("projects", project, "testcases", "generated", "source-draft.json"));
GeneratorExecutionResult execution = await HybridDraftGenerator.BuildAsync(project, source, analysis, included, generatorOptions);
GeneratedSuite suite = execution.Suite;
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(suite, GeneratorJson.Options));
File.WriteAllText(output + ".meta.json", JsonSerializer.Serialize(execution, GeneratorJson.Options));
Console.WriteLine($"Generated {suite.Cases.Count} disabled draft testcases: {output}");
Console.WriteLine($"Provider: {execution.Provider}; AI used: {execution.AiUsed}; fallback: {execution.FellBack}");
Console.WriteLine("Review blocked coverage and assertions before moving the draft into the active testcase folders.");
