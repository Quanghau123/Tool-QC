using AutoTest.Workbooks;
using AutoTest.ProjectAdapters.BeBooking;

namespace AutoTest.Generator.WorkbookImport;

public sealed record WorkbookGenerationItem(string CaseId, bool Generated, string? Json, string? Error);
public sealed record WorkbookGenerationBatch(IReadOnlyList<WorkbookGenerationItem> Items)
{
    public int GeneratedCount => Items.Count(item => item.Generated);
    public int BlockedCount => Items.Count - GeneratedCount;
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
