using System.Text.Json;
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

        IReadOnlyList<WorkbookGenerationItem> generated = batch.Items.Where(item => item.Generated).ToList();
        IReadOnlyList<string> names = WorkbookOutputFiles.Name(generated);
        Equal(generated.Count, names.Count, "output file name per generated item");
        Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "output file names are unique");
        Equal(false, names.Any(name => name.StartsWith("workbook-", StringComparison.OrdinalIgnoreCase)), "output file name is not the case id slug");
        Equal(true, names.All(name => name.EndsWith(".json", StringComparison.Ordinal)), "output file name keeps json extension");

        List<(string Name, string Title)> pairs = generated.Select((item, index) => (names[index], Title(item.Json!))).ToList();
        List<string> sortedByTitle = pairs.OrderBy(pair => pair.Title, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Name).ToList();
        Equal(true, pairs.Select(pair => pair.Name).SequenceEqual(sortedByTitle), "A-Z file sort follows title sort");

        List<WorkbookGenerationItem> colliding =
        [
            new("verify/1", true, Suite("Testcase 1: cùng tiêu đề"), null),
            new("verify/2", true, Suite("Testcase 2: cùng tiêu đề"), null),
        ];
        IReadOnlyList<string> collided = WorkbookOutputFiles.Name(colliding);
        Equal(2, collided.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "colliding titles get distinct file names");
        Equal("cùng-tiêu-đề.json", collided[0], "first colliding title keeps the plain slug");
        Equal("cùng-tiêu-đề-workbook-case-2.json", collided[1], "second colliding title gains its case key");
    }

    private static string Suite(string name) => $$"""
        {"cases":[{"name":"{{name}}"}]}
        """;

    private static string Title(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("cases")[0].GetProperty("name").GetString()!;
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
