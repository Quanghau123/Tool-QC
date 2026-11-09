using System.Text.Json;
using System.Text.RegularExpressions;
using AutoTest.Workbooks;
using AutoTest.ProjectAdapters.BeBooking;

namespace AutoTest.Generator.WorkbookImport;

public sealed record WorkbookGenerationItem(string CaseId, bool Generated, string? Json, string? Error);
public sealed record WorkbookGenerationBatch(IReadOnlyList<WorkbookGenerationItem> Items)
{
    public int GeneratedCount => Items.Count(item => item.Generated);
    public int BlockedCount => Items.Count - GeneratedCount;
}

/// <summary>
/// Owns the file name of a generated suite: named from the testcase title so an A-Z file sort
/// follows the title order (AGENTS.md), and never overwriting an earlier file when two titles
/// slug to the same text.
/// </summary>
public static partial class WorkbookOutputFiles
{
    private const int MaximumSlugLength = 80;

    /// <summary>File name for every generated item, in the same order, deterministic and unique.</summary>
    public static IReadOnlyList<string> Name(IReadOnlyList<WorkbookGenerationItem> items)
    {
        List<string> names = [];
        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkbookGenerationItem item in items)
        {
            string baseName = Slug(Title(item));
            string name = $"{baseName}.json";
            if (!taken.Add(name))
            {
                string suffix = Slug(CaseKey(item.CaseId));
                name = $"{baseName}-{suffix}.json";
                for (int attempt = 2; !taken.Add(name); attempt++) name = $"{baseName}-{suffix}-{attempt}.json";
            }
            names.Add(name);
        }
        return names;
    }

    /// <summary>Slug of a title: lowercase, diacritics kept, non-alphanumeric runs collapse to one '-'.</summary>
    public static string Slug(string? value)
    {
        string slug = NonAlphanumeric().Replace((value ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > MaximumSlugLength) slug = slug[..MaximumSlugLength].TrimEnd('-');
        return slug.Length == 0 ? "testcase" : slug;
    }

    private static string Title(WorkbookGenerationItem item)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(item.Json!);
            string name = document.RootElement.GetProperty("cases")[0].GetProperty("name").GetString() ?? string.Empty;
            return CaseNumberPrefix().Replace(name, string.Empty);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException or ArgumentNullException)
        {
            return item.CaseId;
        }
    }

    private static string CaseKey(string caseId) => $"workbook-case-{caseId.Split('/').Last()}";

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^testcase\s+\d+(?:\.\d+)?\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CaseNumberPrefix();
}

public sealed class WorkbookImportService
{
    private readonly OpenXmlWorkbookReader reader = new();
    private readonly WorkbookAdapterRegistry registry = new();

    public (WorkbookDocument Workbook, WorkbookAnalysis Analysis) Analyze(string project, string workbookPath)
    {
        WorkbookDocument workbook = reader.Read(workbookPath);
        IWorkbookProjectAdapter adapter = registry.Resolve(project);
        return (workbook, adapter.Analyze(workbook));
    }

    public string Generate(string project, string workbookPath, string caseId, string repositoryRoot)
    {
        WorkbookGenerationItem item = GenerateBatch(project, workbookPath, [caseId], repositoryRoot).Items.Single();
        return item.Generated ? item.Json! : throw new InvalidOperationException(item.Error);
    }

    public WorkbookGenerationBatch GenerateBatch(string project, string workbookPath, IReadOnlyCollection<string> caseIds, string repositoryRoot)
    {
        (WorkbookDocument workbook, WorkbookAnalysis analysis) = Analyze(project, workbookPath);
        IWorkbookProjectAdapter adapter = registry.Resolve(project);
        VerifyWorkbookAdapter? verifyAdapter = adapter as VerifyWorkbookAdapter;
        IReadOnlyDictionary<string, VerifyCaseModel>? verifyModels = verifyAdapter is null
            ? null
            : new VerifyWorkbookParser().Parse(workbook).ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        HashSet<string> selected = caseIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<WorkbookGenerationItem> results = [];
        foreach (WorkbookCaseDescriptor descriptor in analysis.Cases.Where(item => selected.Contains(item.Id)))
        {
            if (!descriptor.Supported)
            {
                results.Add(new(descriptor.Id, false, null, descriptor.BlockedReason ?? "Workbook case is blocked."));
                continue;
            }
            try
            {
                string json = verifyModels is not null && verifyModels.TryGetValue(descriptor.Id, out VerifyCaseModel? model)
                    ? new VerifyTestcaseBuilder().Build(model, workbook)
                    : adapter.Generate(workbook, descriptor, repositoryRoot);
                results.Add(new(descriptor.Id, true, json, null));
            }
            catch (InvalidOperationException exception) { results.Add(new(descriptor.Id, false, null, exception.Message)); }
        }
        foreach (string missing in selected.Except(results.Select(item => item.CaseId), StringComparer.OrdinalIgnoreCase))
            results.Add(new(missing, false, null, $"Workbook case '{missing}' was not found."));
        return new(results);
    }
}
