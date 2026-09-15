// CLI generator: phân tích source bằng Roslyn và sinh testcase nháp không phụ thuộc AI.
using System.Text.Json;
using System.Text;
using AutoTest.Generator;
using AutoTest.Generator.Ai;

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

if (args.Contains("--archify-spec", StringComparer.OrdinalIgnoreCase))
{
    string diagramSource = Path.GetFullPath(Argument("--source") ?? string.Empty);
    string diagramType = Argument("--diagram-type") ?? "architecture";
    string visualPreset = Argument("--visual-preset") ?? "classic";
    string animation = Argument("--animation") ?? "none";
    string descriptionPath = Path.GetFullPath(Argument("--description-file") ?? string.Empty);
    string archifyRoot = Path.GetFullPath(Argument("--archify-root") ?? string.Empty);
    string diagramOutput = Path.GetFullPath(Argument("--output") ?? string.Empty);
    if (!Directory.Exists(diagramSource) || !File.Exists(descriptionPath) || !Directory.Exists(archifyRoot) || string.IsNullOrWhiteSpace(diagramOutput))
        throw new InvalidOperationException("Use --archify-spec --source <path> --diagram-type <type> --description-file <file> --archify-root <path> --output <json>.");
    string schemaPath = Path.Combine(archifyRoot, "schemas", $"{diagramType}.schema.json");
    string commonSchemaPath = Path.Combine(archifyRoot, "schemas", "common.schema.json");
    if (!File.Exists(schemaPath) || !File.Exists(commonSchemaPath)) throw new FileNotFoundException("Archify schema was not found.");
    string prompt = $$"""
Create one Archify {{diagramType}} JSON specification from the request below.
Inspect only source and configuration files needed to support the diagram. Never read .env, secrets, tokens, passwords, logs, artifacts, test results, customer data, bin, obj, node_modules, or Git internals.
Use Vietnamese labels. Keep the diagram readable, normally 6-12 primary nodes. Set meta.quality_profile to "showcase", meta.visual_preset to "{{visualPreset}}", and meta.animation to "{{animation}}". Use stable semantic IDs. Add guided views when they improve exploration. Put supporting facts in cards instead of overcrowding edges. Do not invent infrastructure or business flows unsupported by the request or source evidence.
Do not set meta.engineering_profile unless the user explicitly asks for a deployment-ownership engineering review and the source contains complete region, network, ownership, and boundary evidence.
Keep component labels short. For architecture diagrams with more than 8 components, omit component sublabels and place explanatory detail in cards so desktop text remains readable at a 1440px viewport.
Return ONLY the complete JSON object without markdown fences or explanation.

User request:
{{File.ReadAllText(descriptionPath)}}

Diagram schema:
{{File.ReadAllText(schemaPath)}}

Shared schema:
{{File.ReadAllText(commonSchemaPath)}}
""";
    IAiGeneratorProvider provider = AiProviderFactory.Create(Argument("--provider") ?? "codex-cli");
    string specification = StripFence(await provider.ExecuteAsync(diagramSource, prompt, 0));
    using JsonDocument _ = JsonDocument.Parse(specification);
    Directory.CreateDirectory(Path.GetDirectoryName(diagramOutput)!);
    File.WriteAllText(diagramOutput, specification, new UTF8Encoding(false));
    Console.WriteLine(diagramOutput);
    return;
}

if (args.Contains("--archify-repair", StringComparer.OrdinalIgnoreCase))
{
    string diagramSource = Path.GetFullPath(Argument("--source") ?? string.Empty);
    string diagramType = Argument("--diagram-type") ?? "architecture";
    string specificationPath = Path.GetFullPath(Argument("--specification") ?? string.Empty);
    string diagnosticsPath = Path.GetFullPath(Argument("--diagnostics") ?? string.Empty);
    string archifyRoot = Path.GetFullPath(Argument("--archify-root") ?? string.Empty);
    string diagramOutput = Path.GetFullPath(Argument("--output") ?? string.Empty);
    if (!Directory.Exists(diagramSource) || !File.Exists(specificationPath) || !File.Exists(diagnosticsPath) || !Directory.Exists(archifyRoot) || string.IsNullOrWhiteSpace(diagramOutput))
        throw new InvalidOperationException("Use --archify-repair --source <path> --diagram-type <type> --specification <json> --diagnostics <json> --archify-root <path> --output <json>.");
    string schemaPath = Path.Combine(archifyRoot, "schemas", $"{diagramType}.schema.json");
    string commonSchemaPath = Path.Combine(archifyRoot, "schemas", "common.schema.json");
    string prompt = $$"""
Repair this Archify {{diagramType}} JSON specification using the exact Archify diagnostics below.
Inspect source only when needed to verify a factual correction. Never read .env, secrets, tokens, passwords, logs, artifacts other than the supplied files, test results, or customer data.
Change only diagnosed or directly dependent fields. Preserve supported facts and the requested presentation. Never invent deployment regions, networks, owners, boundaries, services, or business flows.
If an optional engineering_profile causes validation errors and the user did not explicitly request that engineering review, remove that profile instead of inventing missing deployment facts.
Return ONLY the complete repaired JSON object without markdown fences or explanation.

Diagnostics:
{{File.ReadAllText(diagnosticsPath)}}

Current specification:
{{File.ReadAllText(specificationPath)}}

Diagram schema:
{{File.ReadAllText(schemaPath)}}

Shared schema:
{{File.ReadAllText(commonSchemaPath)}}
""";
    IAiGeneratorProvider provider = AiProviderFactory.Create(Argument("--provider") ?? "codex-cli");
    string repaired = StripFence(await provider.ExecuteAsync(diagramSource, prompt, 0));
    using JsonDocument _ = JsonDocument.Parse(repaired);
    Directory.CreateDirectory(Path.GetDirectoryName(diagramOutput)!);
    File.WriteAllText(diagramOutput, repaired, new UTF8Encoding(false));
    Console.WriteLine(diagramOutput);
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

static string StripFence(string value)
{
    string result = value.Trim();
    if (!result.StartsWith("```", StringComparison.Ordinal)) return result;
    int firstLine = result.IndexOf('\n');
    int lastFence = result.LastIndexOf("```", StringComparison.Ordinal);
    return firstLine >= 0 && lastFence > firstLine ? result[(firstLine + 1)..lastFence].Trim() : result;
}
