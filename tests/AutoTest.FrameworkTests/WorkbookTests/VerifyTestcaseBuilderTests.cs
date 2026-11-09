using System.Text.Json;
using System.Text.RegularExpressions;
using AutoTest.ProjectAdapters.BeBooking;
using AutoTest.Workbooks;

namespace AutoTest.FrameworkTests.WorkbookTests;

internal static class VerifyTestcaseBuilderTests
{
    public static void Run(string repositoryRoot)
    {
        AssertSourceTopologyAndPayload();
        AssertIntegrityEvidence();
        AssertLifecycleLimitations();
        AssertConcurrencyLimitations();
        AssertAuthenticationBoundary();
        AssertNamingRule();

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

    private static void AssertSourceTopologyAndPayload()
    {
        VerifyDailySchedule dayOne = new(
            "day1",
            18,
            ["06:00", "06:08", "06:32"],
            new Dictionary<string, int> { ["06:00"] = 6, ["06:08"] = 5, ["06:32"] = 7 },
            [
                new VerifyDurationGroup(11, 16, ["06:00", "06:08"], new Dictionary<string, int> { ["06:00"] = 6, ["06:08"] = 5 }),
                new VerifyDurationGroup(7, 16, ["06:32"], new Dictionary<string, int> { ["06:32"] = 7 }),
            ]);
        VerifyDailySchedule dayTwo = dayOne with { DateKey = "day2" };
        VerifyCaseModel model = new(
            "verify/999",
            "Duration nhiều range rời",
            [new VerifyCampaignModel("TEST Duration", "Duration", 1, [dayOne, dayTwo])],
            new VerifyExpectation(true, "PASS", false),
            "day2-to-day1",
            999);
        WorkbookDocument workbook = new("source-backed.xlsx", "sha", 1, []);

        string json = new VerifyTestcaseBuilder().Build(model, workbook);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement steps = document.RootElement.GetProperty("cases")[0].GetProperty("steps");
        JsonElement createBody = steps.EnumerateArray()
            .Single(step => step.GetProperty("name").GetString()!.StartsWith("Tạo campaign đích", StringComparison.Ordinal))
            .GetProperty("request").GetProperty("body");
        JsonElement createFrames = createBody.GetProperty("schedules")[0].GetProperty("devices")[0].GetProperty("timeFrames");
        JsonElement verifyFrames = steps.EnumerateArray()
            .Single(step => step.GetProperty("request").TryGetProperty("path", out JsonElement path) && path.GetString() == "/api/Campaigns/VerifySlots")
            .GetProperty("request").GetProperty("body").GetProperty("schedules")[0].GetProperty("devices")[0].GetProperty("timeFrames");

        Equal(3, createFrames.GetArrayLength(), "duration create keeps all disjoint frames");
        Equal("${tf1}", createFrames[0].GetProperty("timeFrameId").GetString(), "duration first frame offset");
        Equal("${tf2}", createFrames[1].GetProperty("timeFrameId").GetString(), "duration consecutive frame offset");
        Equal("${tf5}", createFrames[2].GetProperty("timeFrameId").GetString(), "duration gap frame offset");
        Equal(6, createFrames[0].GetProperty("spotCount").GetInt32(), "duration first frame allocation");
        Equal(5, createFrames[1].GetProperty("spotCount").GetInt32(), "duration second frame allocation");
        Equal(7, createFrames[2].GetProperty("spotCount").GetInt32(), "duration third frame allocation");
        Equal(createFrames[0].GetProperty("timeFrameGroupKey").GetString(), createFrames[1].GetProperty("timeFrameGroupKey").GetString(), "consecutive frames share group key");
        Equal(false, createFrames[0].GetProperty("timeFrameGroupKey").GetString() == createFrames[2].GetProperty("timeFrameGroupKey").GetString(), "disjoint ranges use distinct group keys");
        Equal(1, createFrames[0].GetProperty("timeFrameGroupOrder").GetInt32(), "first group order starts at one");
        Equal(2, createFrames[1].GetProperty("timeFrameGroupOrder").GetInt32(), "consecutive group order increments");
        Equal(1, createFrames[2].GetProperty("timeFrameGroupOrder").GetInt32(), "second group order restarts");
        Equal("${scheduleDate}", FindVerifyScheduleDate(steps), "reverse sync verifies first day");
        Equal(3, verifyFrames.GetArrayLength(), "verify preserves duration topology");
        Equal(false, json.Contains("RequestedSpotCount", StringComparison.Ordinal), "builder excludes obsolete requested spot field");
        Equal(false, json.Contains("AllocationMode", StringComparison.Ordinal), "builder excludes obsolete allocation mode field");
        Equal(true, createBody.GetProperty("schedules")[1].GetProperty("devices")[0].GetProperty("timeFrames")[0].TryGetProperty("timeFrameGroupKey", out _), "second day preserves groups");
        Equal(json, new VerifyTestcaseBuilder().Build(model, workbook), "source-backed deterministic output");

        VerifyCaseModel spotModel = model with
        {
            Campaigns = [new VerifyCampaignModel("TEST Spot", "Spot", 1, [dayOne with { Groups = dayOne.Groups }, dayTwo with { Groups = dayTwo.Groups }])],
        };
        using JsonDocument spotDocument = JsonDocument.Parse(new VerifyTestcaseBuilder().Build(spotModel, workbook));
        JsonElement spotFrames = spotDocument.RootElement.GetProperty("cases")[0].GetProperty("steps").EnumerateArray()
            .Single(step => step.GetProperty("name").GetString()!.StartsWith("Tạo campaign đích", StringComparison.Ordinal))
            .GetProperty("request").GetProperty("body").GetProperty("schedules")[0].GetProperty("devices")[0].GetProperty("timeFrames");
        Equal(true, spotFrames[0].TryGetProperty("timeFrameGroupKey", out _), "spot keeps source group key");
        Equal(2, spotFrames[1].GetProperty("timeFrameGroupOrder").GetInt32(), "spot keeps source group order");
    }

    private static void AssertIntegrityEvidence()
    {
        VerifyTestcaseBuilder builder = new();
        WorkbookDocument workbook = new("verify.xlsx", "sha256", 0, []);

        string validJson = builder.Build(IntegrityModel(isValid: true), workbook);
        using JsonDocument valid = JsonDocument.Parse(validJson);
        string validEvidence = EvidenceCommand(valid.RootElement.GetProperty("cases")[0].GetProperty("steps"));
        Equal(true, validEvidence.Contains("public.campaign_schedule", StringComparison.Ordinal), "valid integrity checks schedules");
        Equal(true, validEvidence.Contains("public.campaign_allocation", StringComparison.Ordinal), "valid integrity checks allocations");
        Equal(true, validEvidence.Contains("(SELECT COUNT(*) FROM candidate)=1", StringComparison.Ordinal), "valid integrity checks candidate campaign");

        string invalidJson = builder.Build(IntegrityModel(isValid: false), workbook);
        using JsonDocument invalid = JsonDocument.Parse(invalidJson);
        string invalidEvidence = EvidenceCommand(invalid.RootElement.GetProperty("cases")[0].GetProperty("steps"));
        Equal(true, invalidEvidence.Contains("(SELECT COUNT(*) FROM candidate)", StringComparison.Ordinal), "invalid integrity checks candidate campaign");
        Equal(true, invalidEvidence.Contains("public.campaign_schedule", StringComparison.Ordinal), "invalid integrity checks schedules");
        Equal(true, invalidEvidence.Contains("public.campaign_allocation", StringComparison.Ordinal), "invalid integrity checks allocations");
        Equal("0", invalid.RootElement.GetProperty("cases")[0].GetProperty("steps").EnumerateArray().Last().GetProperty("expect").GetProperty("database").GetProperty("scalarEquals").GetString(), "invalid integrity rejects partial persistence");
    }

    private static void AssertLifecycleLimitations()
    {
        VerifyTestcaseBuilder builder = new();
        WorkbookDocument workbook = new("verify.xlsx", "sha256", 0, []);

        for (int number = 41; number <= 46; number++)
        {
            VerifyCaseModel model = IntegrityModel(isValid: true) with { Id = $"verify/{number}" };
            string json = builder.Build(model, workbook);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement lifecycle = document.RootElement.GetProperty("cases")[0].GetProperty("coverage").GetProperty("lifecycle");

            Equal("notApplicable", lifecycle.GetProperty("status").GetString(), $"case {number} lifecycle status");
            Equal(true, lifecycle.GetProperty("reason").GetString()!.Contains($"case {number}", StringComparison.Ordinal), $"case {number} lifecycle source rationale");
            Equal(false, json.Contains("ApproveMany", StringComparison.Ordinal), $"case {number} does not infer approval action");
            Equal(false, json.Contains("RejectMany", StringComparison.Ordinal), $"case {number} does not infer reject action");
            Equal(false, json.Contains("CancelMany", StringComparison.Ordinal), $"case {number} does not infer cancel action");
        }
    }

    private static void AssertConcurrencyLimitations()
    {
        VerifyTestcaseBuilder builder = new();
        WorkbookDocument workbook = new("verify.xlsx", "sha256", 0, []);

        foreach (int number in new[] { 1, 41, 68 })
        {
            VerifyCaseModel model = IntegrityModel(isValid: true) with { Id = $"verify/{number}" };
            string json = builder.Build(model, workbook);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement concurrency = document.RootElement.GetProperty("cases")[0].GetProperty("coverage").GetProperty("concurrency");

            Equal("notApplicable", concurrency.GetProperty("status").GetString(), $"case {number} concurrency status");
            Equal(true, concurrency.GetProperty("reason").GetString()!.Contains($"case {number}", StringComparison.Ordinal), $"case {number} concurrency source rationale");
            Equal(false, json.Contains("concurrentRequests", StringComparison.Ordinal), $"case {number} does not infer concurrent requests");
            Equal(false, json.Contains("parallelRequests", StringComparison.Ordinal), $"case {number} does not infer parallel requests");
        }
    }

    private static void AssertAuthenticationBoundary()
    {
        string json = new VerifyTestcaseBuilder().Build(IntegrityModel(isValid: true) with { Id = "verify/7" }, new WorkbookDocument("verify.xlsx", "sha256", 0, []));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement testCase = document.RootElement.GetProperty("cases")[0];
        JsonElement scope = testCase.GetProperty("coverage").GetProperty("scope");
        JsonElement protectedRequest = testCase.GetProperty("steps").EnumerateArray()
            .First(step => step.GetProperty("request").TryGetProperty("path", out JsonElement path) && path.GetString() == "/api/Campaigns/VerifySlots");

        Equal("admin", protectedRequest.GetProperty("auth").GetString(), "verify uses environment-backed admin alias");
        Equal("notApplicable", scope.GetProperty("status").GetString(), "scope status");
        Equal(true, scope.GetProperty("reason").GetString()!.Contains("case 7", StringComparison.Ordinal), "scope source rationale");
        Equal(false, json.Contains("authToken", StringComparison.Ordinal), "generated suite has no auth token literal");
        Equal(false, json.Contains("password", StringComparison.OrdinalIgnoreCase), "generated suite has no password literal");
    }

    /// <summary>AGENTS.md naming rule coverage that needs no workbook file: synthetic models only.</summary>
    private static void AssertNamingRule()
    {
        VerifyTestcaseBuilder builder = new();
        WorkbookDocument workbook = new("synthetic.xlsx", "sha", 1, []);

        string clean = CaseName(builder.Build(NamingModel("verify/42", "Case 3: Cả 2 ngày dùng chung khung giờ"), workbook));
        Equal(true, Regex.IsMatch(clean, @"^Testcase \d+: \S"), $"clean title keeps Testcase X: N form ('{clean}')");
        Equal(false, clean.Contains("VERIFY - ", StringComparison.Ordinal), "clean title drops the VERIFY prefix");
        Equal(false, clean.Contains("Case 3", StringComparison.Ordinal), "clean title has no Case N leftover");
        Equal("Testcase 42: Cả 2 ngày dùng chung khung giờ", clean, "clean title is composed from the workbook number");

        string dirty = CaseName(builder.Build(NamingModel("verify/54", "Case 5:  Cả 2 ngày   Duration và Spot   "), workbook));
        Equal("Testcase 54: Cả 2 ngày Duration và Spot", dirty, "dirty title collapses whitespace and trims");
        Equal(false, dirty.Contains("  ", StringComparison.Ordinal), "dirty title has no double space");

        string untitled = CaseName(builder.Build(NamingModel("verify/67", "Case 67"), workbook));
        Equal(true, Regex.IsMatch(untitled, @"^Testcase 67: \S"), $"untitled case keeps Testcase X: N form ('{untitled}')");
        Equal(false, untitled.Contains("Case 67", StringComparison.Ordinal), "untitled case has no bare Case N title");
        Equal(true, untitled.Contains("VerifySlots", StringComparison.Ordinal), "untitled case names the verified endpoint");
    }

    private static VerifyCaseModel NamingModel(string id, string title) => new(
        id,
        title,
        [Campaign("DURATION NEN", "Duration"), Campaign("TEST SPOT", "Spot")],
        new VerifyExpectation(true, null, false),
        "day1-to-day2",
        10);

    private static VerifyCampaignModel Campaign(string name, string kind) => new(
        name,
        kind,
        1,
        [
            new("day1", 2, ["06:00", "06:08"], new Dictionary<string, int> { ["06:00"] = 1, ["06:08"] = 1 },
                [new VerifyDurationGroup(2, 5, ["06:00", "06:08"], new Dictionary<string, int> { ["06:00"] = 1, ["06:08"] = 1 })]),
            new("day2", 2, ["06:00", "06:08"], new Dictionary<string, int> { ["06:00"] = 1, ["06:08"] = 1 },
                [new VerifyDurationGroup(2, 5, ["06:00", "06:08"], new Dictionary<string, int> { ["06:00"] = 1, ["06:08"] = 1 })]),
        ]);

    private static string CaseName(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("cases")[0].GetProperty("name").GetString()!;
    }

    private static VerifyCaseModel IntegrityModel(bool isValid) => new(
        "verify/99",
        "Kiểm tra integrity",
        [new VerifyCampaignModel("Ứng viên", "Spot", 1, [new VerifyDailySchedule("day1", 1, ["08:00"], new Dictionary<string, int> { ["08:00"] = 1 }, [])])],
        new VerifyExpectation(isValid, null, true),
        "day1-to-day2",
        1);

    private static string? FindVerifyScheduleDate(JsonElement steps) => steps.EnumerateArray()
        .Single(step => step.GetProperty("request").TryGetProperty("path", out JsonElement path) && path.GetString() == "/api/Campaigns/VerifySlots")
        .GetProperty("request").GetProperty("body").GetProperty("schedules")[0].GetProperty("date").GetString();

    private static string EvidenceCommand(JsonElement steps) => steps.EnumerateArray().Last()
        .GetProperty("request").GetProperty("database").GetProperty("command").GetString()!;

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
    }
}
