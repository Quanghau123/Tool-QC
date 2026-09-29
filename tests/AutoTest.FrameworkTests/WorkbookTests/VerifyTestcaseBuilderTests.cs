using System.Text.Json;
using AutoTest.ProjectAdapters.BeBooking;
using AutoTest.Workbooks;

namespace AutoTest.FrameworkTests.WorkbookTests;

internal static class VerifyTestcaseBuilderTests
{
    public static void Run(string repositoryRoot)
    {
        string workbookPath = Path.Combine(repositoryRoot, ".artifacts", "Campaign (1).xlsx");
        if (!File.Exists(workbookPath)) return;
        WorkbookDocument workbook = new OpenXmlWorkbookReader().Read(workbookPath);
        IReadOnlyList<VerifyCaseModel> models = new VerifyWorkbookParser().Parse(workbook);
        VerifyTestcaseBuilder builder = new();

        string case54Json = builder.Build(models.Single(item => item.Id == "verify/54"), workbook);
        using JsonDocument case54 = JsonDocument.Parse(case54Json);
        JsonElement steps = case54.RootElement.GetProperty("cases")[0].GetProperty("steps");
        Equal(2, case54.RootElement.GetProperty("schemaVersion").GetInt32(), "builder schema v2");
        Equal(true, case54Json.Contains("${tf5}", StringComparison.Ordinal), "builder preserves TimeFrame gaps");
        Equal(false, case54Json.Contains("06:00", StringComparison.Ordinal), "builder has no fixed 06:00 time");
        Equal(true, case54Json.Contains("CURRENT_DATE\\u002B1", StringComparison.Ordinal), "builder starts on tomorrow");
        Equal(true, case54Json.Contains("CURRENT_DATE\\u002B2", StringComparison.Ordinal), "builder uses following day");
        Equal(false, case54Json.Contains("CURRENT_DATE\\u002B7", StringComparison.Ordinal), "builder has no seven-day offset");
        Equal(true, steps.EnumerateArray().Any(step => step.GetProperty("name").GetString()!.StartsWith("Tạo campaign đích", StringComparison.Ordinal)), "valid case creates target after verify");
        Equal("${scheduleDate}", FindVerifyScheduleDate(steps), "reverse sync verifies day 1");
        Equal("1", steps.EnumerateArray().Last().GetProperty("expect").GetProperty("database").GetProperty("scalarEquals").GetString(), "valid target persistence evidence");
        Equal(case54Json, builder.Build(models.Single(item => item.Id == "verify/54"), workbook), "builder deterministic output");

        string rejectedJson = builder.Build(models.Single(item => item.Id == "verify/17"), workbook);
        using JsonDocument rejected = JsonDocument.Parse(rejectedJson);
        JsonElement rejectedSteps = rejected.RootElement.GetProperty("cases")[0].GetProperty("steps");
        Equal(false, rejectedSteps.EnumerateArray().Any(step => step.GetProperty("name").GetString()!.StartsWith("Tạo campaign đích", StringComparison.Ordinal)), "rejected case does not create target");
        Equal("0", rejectedSteps.EnumerateArray().Last().GetProperty("expect").GetProperty("database").GetProperty("scalarEquals").GetString(), "rejected case no partial persistence evidence");
    }

    private static string? FindVerifyScheduleDate(JsonElement steps) => steps.EnumerateArray()
        .Single(step => step.GetProperty("request").TryGetProperty("path", out JsonElement path) && path.GetString() == "/api/Campaigns/VerifySlots")
        .GetProperty("request").GetProperty("body").GetProperty("schedules")[0].GetProperty("date").GetString();

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
