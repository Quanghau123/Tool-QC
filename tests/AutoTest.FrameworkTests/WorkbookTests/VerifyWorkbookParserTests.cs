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

        AssertActiveWorkbookManifest();
        AssertSparseCaseIsBlocked();
    }

    private static void AssertSparseCaseIsBlocked()
    {
        var cells = new Dictionary<string, WorkbookCell>
        {
            ["A1"] = new("A1", 1, 1, "VERIFY - Case 99: bảng thiếu ngày 2", null, null, 0),
            ["A2"] = new("A2", 2, 1, "Campaign day 1", null, null, 0),
            ["B2"] = new("B2", 2, 2, "Spot", null, null, 0),
            ["C2"] = new("C2", 2, 3, "06:00-06:08", null, null, 0)
        };
        var workbook = new WorkbookDocument("sparse.xlsx", "sparse", 0,
            [new WorkbookSheet("VERIFY", false, cells, [], 0)]);

        VerifyCaseModel sparseCase = new VerifyWorkbookParser().Parse(workbook).Single();
        Equal("unknown", sparseCase.SyncDirection, "sparse case unknown direction");
        Equal("unknown", sparseCase.Source!.SemanticClass, "sparse case unknown semantic class");
        Equal(null, sparseCase.Source.Direction, "sparse case manifest direction");
        Equal(1, sparseCase.Source.DayTables[0].FrameLabels.Count, "sparse case day 1 frames");
        Equal(0, sparseCase.Source.DayTables[1].FrameLabels.Count, "sparse case day 2 frames");
        Equal("TQC-XLSX-BE-MISSING-DAY2", sparseCase.Diagnostics!.Single().Code, "sparse case missing-day diagnostic");

        WorkbookAnalysis analysis = new VerifyWorkbookAdapter().Analyze(workbook);
        Equal(false, analysis.Cases.Single().Supported, "sparse case adapter blocked");
        Equal("TQC-XLSX-BE-MISSING-DAY2", analysis.Diagnostics.Single().Code, "sparse case adapter diagnostic");
    }

    private static void AssertActiveWorkbookManifest()
    {
        const string activeWorkbookPath = "C:/Users/quanghau/Downloads/Campaign.xlsx";
        if (!File.Exists(activeWorkbookPath)) return;

        WorkbookDocument workbook = new OpenXmlWorkbookReader().Read(activeWorkbookPath);
        Equal("785e29e5b2809af410977c129bf4c6e758c52228e49ec0638dd2b40d1c61f04f", workbook.Sha256, "active VERIFY workbook fingerprint");

        var analysis = new VerifyWorkbookAdapter().Analyze(workbook);
        (string Id, string SourceRange)[] expected = ActiveManifest;
        Equal(expected.Length, analysis.Cases.Count, "active VERIFY manifest case count");
        Equal(63, analysis.Cases.Count, "active VERIFY manifest expected case count");
        Equal(0, analysis.Diagnostics.Count, "active VERIFY manifest diagnostics");
        for (int index = 0; index < expected.Length; index++)
        {
            Equal(expected[index].Id, analysis.Cases[index].Id, $"active VERIFY manifest ID at {index}");
            Equal(expected[index].SourceRange, analysis.Cases[index].SourceRange, $"active VERIFY manifest source range for {expected[index].Id}");
        }
    }

    private static readonly (string Id, string SourceRange)[] ActiveManifest =
    [
        ("verify/1", "A1:Z21"), ("verify/2", "A22:Z42"), ("verify/3", "A43:Z63"), ("verify/4", "A64:Z84"), ("verify/5", "A85:Z105"),
        ("verify/6", "A106:Z126"), ("verify/7", "A127:Z147"), ("verify/8", "A148:Z168"), ("verify/9", "A169:Z189"), ("verify/10", "A190:Z210"),
        ("verify/11", "A211:Z223"), ("verify/12", "A224:Z238"), ("verify/13", "A239:Z251"), ("verify/14", "A252:Z268"), ("verify/15", "A269:Z288"),
        ("verify/16", "A289:Z301"), ("verify/17", "A302:Z314"), ("verify/18", "A315:Z329"), ("verify/19", "A330:Z342"), ("verify/20", "A343:Z357"),
        ("verify/21", "A358:Z372"), ("verify/22", "A373:Z385"), ("verify/23", "A386:Z402"), ("verify/24", "A403:Z417"), ("verify/25", "A418:Z446"),
        ("verify/26", "A447:Z468"), ("verify/27", "A469:Z490"), ("verify/28", "A491:Z512"), ("verify/29", "A513:Z534"), ("verify/30", "A535:Z550"),
        ("verify/33", "A551:Z572"), ("verify/34", "A573:Z594"), ("verify/35", "A595:Z616"), ("verify/36", "A617:Z636"), ("verify/37", "A637:Z656"),
        ("verify/38", "A657:Z678"), ("verify/39", "A679:Z706"), ("verify/40", "A707:Z730"), ("verify/41", "A731:Z752"), ("verify/42", "A753:Z774"),
        ("verify/43", "A775:Z796"), ("verify/44", "A797:Z818"), ("verify/45", "A819:Z840"), ("verify/46", "A841:Z862"), ("verify/47", "A863:Z884"),
        ("verify/48", "A885:Z906"), ("verify/49", "A907:Z940"), ("verify/50", "A941:Z962"), ("verify/51", "A963:Z987"), ("verify/52", "A988:Z1009"),
        ("verify/53", "A1010:Z1031"), ("verify/54", "A1032:Z1051"), ("verify/55", "A1052:Z1071"), ("verify/56", "A1072:Z1091"), ("verify/57", "A1092:Z1111"),
        ("verify/61", "A1112:Z1131"), ("verify/62", "A1132:Z1152"), ("verify/63", "A1153:Z1173"), ("verify/64", "A1174:Z1193"), ("verify/65", "A1194:Z1216"),
        ("verify/66", "A1217:Z1230"), ("verify/67", "A1231:Z1245"), ("verify/68", "A1246:Z1256")
    ];

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
