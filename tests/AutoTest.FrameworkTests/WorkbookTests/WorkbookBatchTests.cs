using AutoTest.Generator.WorkbookImport;

namespace AutoTest.FrameworkTests.WorkbookTests;

internal static class WorkbookBatchTests
{
    public static void Run(string repositoryRoot)
    {
        string workbookPath = Path.Combine(repositoryRoot, ".artifacts", "Campaign (1).xlsx");
        if (!File.Exists(workbookPath)) return;
        WorkbookGenerationBatch batch = new WorkbookImportService().GenerateBatch("be-booking", workbookPath, ["verify/54", "verify/missing", "verify/54", "verify/1"], repositoryRoot);
        Equal(3, batch.Items.Count, "batch de-duplicates selected IDs");
        Equal("verify/1", batch.Items[0].CaseId, "batch keeps workbook order");
        Equal("verify/54", batch.Items[1].CaseId, "batch keeps later workbook order");
        Equal(true, batch.Items[0].Generated, "batch generates supported case");
        Equal(false, batch.Items[2].Generated, "batch blocks missing case only");
        Equal(2, batch.GeneratedCount, "batch generated count");
        Equal(1, batch.BlockedCount, "batch blocked count");
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
