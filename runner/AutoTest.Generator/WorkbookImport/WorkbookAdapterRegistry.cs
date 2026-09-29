using AutoTest.ProjectAdapters.BeBooking;
using AutoTest.Workbooks;

namespace AutoTest.Generator.WorkbookImport;

public sealed class WorkbookAdapterRegistry
{
    private readonly IReadOnlyList<IWorkbookProjectAdapter> adapters = [new VerifyWorkbookAdapter()];

    public IWorkbookProjectAdapter Resolve(string project) => adapters.FirstOrDefault(adapter => adapter.Project.Equals(project, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"No workbook adapter is configured for project '{project}'.");
}
