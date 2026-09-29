using System.Text.Json;

namespace AutoTest.FrameworkTests.WorkbookTests;

internal static class WorkbookTestHarness
{
    public static void Run(string repositoryRoot)
    {
        string fixturePath = Path.Combine(repositoryRoot, "tests", "AutoTest.FrameworkTests", "Fixtures", "verify-oracle.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        JsonElement cases = document.RootElement.GetProperty("cases");
        Equal(4, cases.GetArrayLength(), "workbook oracle representative case count");
        Equal("verify/54", cases[2].GetProperty("id").GetString(), "workbook oracle case 54 identity");
        Equal(false, cases[1].GetProperty("expectedValid").GetBoolean(), "workbook oracle rejected Spot case");
        Equal("day2-to-day1", cases[2].GetProperty("syncDirection").GetString(), "workbook oracle reverse direction");
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
