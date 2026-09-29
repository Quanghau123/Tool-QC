using System.Text.Json;
using AutoTest.ProjectAdapters.BeBooking;
using AutoTest.Workbooks;

namespace AutoTest.FrameworkTests.WorkbookTests;

internal static class VerifyWorkbookParserTests
{
    public static void Run(string repositoryRoot)
    {
        string workbookPath = Path.Combine(repositoryRoot, ".artifacts", "Campaign (1).xlsx");
        if (!File.Exists(workbookPath)) return;
        IReadOnlyList<VerifyCaseModel> cases = new VerifyWorkbookParser().Parse(new OpenXmlWorkbookReader().Read(workbookPath));
        Equal(69, cases.Count, "VERIFY parser case count");
        Equal(69, cases.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "VERIFY stable IDs");
        VerifyCaseModel case54 = cases.Single(item => item.Id == "verify/54");
        Equal("day2-to-day1", case54.SyncDirection, "Case 54 reverse sync");
        Equal(3, case54.Campaigns.Count, "Case 54 campaign count");
        VerifyCaseModel case17 = cases.Single(item => item.Id == "verify/17");
        Equal(false, case17.Expectation.IsValid, "Case 17 rejected expectation");

        string oraclePath = Path.Combine(repositoryRoot, ".artifacts", "verify-v21-reextracted.json");
        if (!File.Exists(oraclePath)) return;
        using JsonDocument oracle = JsonDocument.Parse(File.ReadAllText(oraclePath));
        Equal(oracle.RootElement.GetArrayLength(), cases.Count, "VERIFY oracle parity count");
        foreach (JsonElement expected in oracle.RootElement.EnumerateArray())
        {
            string id = expected.GetProperty("id").GetString()!.Replace("VERIFY-V21-CASE-", "verify/", StringComparison.Ordinal);
            VerifyCaseModel actual = cases.Single(item => item.Id == id);
            Equal(expected.GetProperty("campaigns").GetArrayLength(), actual.Campaigns.Count, $"{id} campaign count");
            Equal(expected.GetProperty("verification").GetProperty("expectedTargetIsValid").GetBoolean(), actual.Expectation.IsValid, $"{id} validity");
            Equal(expected.GetProperty("verification").GetProperty("syncDirection").GetString(), actual.SyncDirection, $"{id} direction");
        }
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
