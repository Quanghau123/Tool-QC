// CLI generator: phân tích source bằng Roslyn và sinh testcase nháp không phụ thuộc AI.
using System.Text.Json;
using AutoTest.Generator;

string? Argument(string key)
{
    int index = Array.FindIndex(args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("AutoTest.Generator --source <path> --project <name> [--analyze] [--include <id,id>] [--output <file.json>] [--coverage-report <coverage.cobertura.xml>]");
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

GeneratorAnalysis analysis = new SourceContractAnalyzer().Analyze(source);
string? analysisOutput = Argument("--analysis-output");
if (args.Contains("--analyze", StringComparer.OrdinalIgnoreCase))
{
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

HashSet<string>? included = Argument("--include")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
string output = Path.GetFullPath(Argument("--output") ?? Path.Combine("projects", project, "testcases", "generated", "source-draft.json"));
GeneratedSuite suite = DraftSuiteBuilder.Build(project, analysis, included);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(suite, GeneratorJson.Options));
Console.WriteLine($"Generated {suite.Cases.Count} disabled draft testcases: {output}");
Console.WriteLine("Review blocked coverage and assertions before moving the draft into the active testcase folders.");
