using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoTest.Workbooks;

namespace AutoTest.ProjectAdapters.BeBooking;

public sealed class VerifyTestcaseBuilder
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Build(VerifyCaseModel model, WorkbookDocument workbook)
    {
        string number = model.Id.Split('/').Last();
        VerifyCampaignModel target = model.Campaigns[^1];
        int firstFrameMinutes = model.Campaigns.SelectMany(item => item.DailySchedules).SelectMany(item => item.FrameLabels).Select(FrameMinutes).Min();
        int frameCount = model.Campaigns.SelectMany(item => item.DailySchedules).SelectMany(item => item.FrameLabels).Select(label => FrameOffset(label, firstFrameMinutes)).Max() + 1;
        string targetDateKey = model.SyncDirection == "day2-to-day1" ? "day1" : "day2";
        string verifyStepName = model.Expectation.IsValid ? "Verify campaign đích đúng expectation workbook" : "Verify campaign đích bị từ chối đúng expectation workbook";
        string evidenceStepName = model.Expectation.IsValid ? "Xác nhận campaign đích được lưu sau Verify hợp lệ" : "Xác nhận Verify bị từ chối không lưu Campaign một phần";
        JsonArray steps = [FixtureStep(frameCount)];
        for (int index = 0; index < model.Campaigns.Count - 1; index++)
            steps.Add(CreateCampaignStep(model.Campaigns[index], index + 1, number, firstFrameMinutes, false));
        steps.Add(VerifyStep(target, targetDateKey, verifyStepName, model.Expectation.IsValid, firstFrameMinutes));
        if (model.Expectation.IsValid)
            steps.Add(CreateCampaignStep(target, model.Campaigns.Count, number, firstFrameMinutes, true));
        steps.Add(EvidenceStep(evidenceStepName, model.Expectation.IsValid));

        JsonObject suite = new()
        {
            ["schemaVersion"] = 2,
            ["project"] = "be-booking",
            ["workbookSource"] = new JsonObject
            {
                ["fileName"] = workbook.FileName,
                ["sha256"] = workbook.Sha256,
                ["caseId"] = model.Id,
                ["sourceRow"] = model.SourceRow,
            },
            ["cases"] = new JsonArray(new JsonObject
            {
                ["id"] = $"campaign.workbook.verify-case-{number}",
                ["name"] = $"VERIFY - {model.Title}",
                ["tags"] = new JsonArray("campaign-workbook", "campaign-verify-slots", $"verify-case-{number}"),
                ["destructive"] = true,
                ["variables"] = new JsonObject { ["candidateName"] = $"QC-VERIFY-{number}-${{unique}}" },
                ["steps"] = steps,
                ["coverage"] = Coverage(verifyStepName, evidenceStepName),
            }),
        };
        return suite.ToJsonString(Options) + Environment.NewLine;
    }

    private static JsonObject FixtureStep(int frameCount)
    {
        string pivots = string.Join(',', Enumerable.Range(1, frameCount).Select(index => $"MAX(CASE WHEN rn={index} THEN \"Id\"::text END) AS \"tf{index}\""));
        string command = $"WITH frames AS (SELECT tf.\"Id\",ROW_NUMBER() OVER(ORDER BY tf.\"StartTime\") rn FROM public.time_frame tf WHERE tf.\"IsActive\"=true AND tf.\"Version\"=2), device AS (SELECT d.\"Id\"::text AS device_id FROM public.device d WHERE d.\"OperationStatus\"=1 ORDER BY d.\"CreatedAt\" DESC LIMIT 1), creative AS (SELECT cr.\"Id\"::text AS creative_id,cr.\"FieldId\"::text AS field_id FROM public.creative cr WHERE cr.\"MtcMediaId\" IS NOT NULL AND cr.\"SyncStatus\"=3 AND cr.\"IsDeleted\"=false ORDER BY cr.\"CreatedAt\" DESC LIMIT 1) SELECT (CURRENT_DATE+1)::text AS \"scheduleDate\",(CURRENT_DATE+2)::text AS \"scheduleDate2\",device.device_id AS \"deviceId\",creative.creative_id AS \"creativeId\",creative.field_id AS \"fieldId\",{pivots} FROM frames CROSS JOIN device CROSS JOIN creative GROUP BY device.device_id,creative.creative_id,creative.field_id HAVING COUNT(*)>={frameCount}";
        JsonObject step = DatabaseStep("Chọn fixture động theo thời điểm chạy", command, true, null);
        JsonObject save = new()
        {
            ["scheduleDate"] = "$.rows.0.scheduleDate",
            ["scheduleDate2"] = "$.rows.0.scheduleDate2",
            ["deviceId"] = "$.rows.0.deviceId",
            ["creativeId"] = "$.rows.0.creativeId",
            ["fieldId"] = "$.rows.0.fieldId",
        };
        for (int index = 1; index <= frameCount; index++) save[$"tf{index}"] = $"$.rows.0.tf{index}";
        step["save"] = save;
        return step;
    }

    private static JsonObject CreateCampaignStep(VerifyCampaignModel campaign, int index, string caseNumber, int firstFrameMinutes, bool candidate)
    {
        JsonArray schedules = [];
        foreach (VerifyDailySchedule schedule in campaign.DailySchedules)
            schedules.Add(ScheduleNode(campaign, schedule, $"{caseNumber}-{index}", firstFrameMinutes));
        return new JsonObject
        {
            ["name"] = candidate ? $"Tạo campaign đích {campaign.Name}" : $"Tạo campaign nền {campaign.Name}",
            ["auth"] = "admin",
            ["request"] = new JsonObject
            {
                ["method"] = "POST",
                ["path"] = "/api/Campaigns",
                ["body"] = new JsonObject
                {
                    ["name"] = candidate ? "${candidateName}" : $"QC-VERIFY-{caseNumber}-BASE-{index}-${{unique}}",
                    ["customerName"] = $"QC workbook {caseNumber}",
                    ["sourceType"] = 1,
                    ["type"] = campaign.Kind.Equals("Spot", StringComparison.OrdinalIgnoreCase) ? 1 : 2,
                    ["creativeId"] = "${creativeId}",
                    ["fieldId"] = "${fieldId}",
                    ["runStart"] = "${scheduleDate}",
                    ["runEnd"] = "${scheduleDate2}",
                    ["schedules"] = schedules,
                },
            },
            ["expect"] = new JsonObject { ["status"] = 200, ["json"] = new JsonObject { ["$.data.id"] = new JsonObject { ["exists"] = true } } },
        };
    }

    private static JsonObject VerifyStep(VerifyCampaignModel campaign, string targetDateKey, string name, bool expectedValid, int firstFrameMinutes)
    {
        VerifyDailySchedule target = campaign.DailySchedules.FirstOrDefault(item => item.DateKey == targetDateKey) ?? campaign.DailySchedules[^1];
        return new JsonObject
        {
            ["name"] = name,
            ["auth"] = "admin",
            ["request"] = new JsonObject
            {
                ["method"] = "POST",
                ["path"] = "/api/Campaigns/VerifySlots",
                ["body"] = new JsonObject
                {
                    ["type"] = campaign.Kind.Equals("Spot", StringComparison.OrdinalIgnoreCase) ? 1 : 2,
                    ["schedules"] = new JsonArray(ScheduleNode(campaign, target, "verify", firstFrameMinutes)),
                },
            },
            ["expect"] = new JsonObject { ["status"] = 200, ["json"] = new JsonObject { ["$.data.isValid"] = new JsonObject { ["equals"] = expectedValid } } },
        };
    }

    private static JsonObject ScheduleNode(VerifyCampaignModel campaign, VerifyDailySchedule schedule, string seed, int firstFrameMinutes)
    {
        JsonArray frames = [];
        foreach (string label in schedule.FrameLabels) frames.Add(FrameNode(campaign, schedule, label, seed, firstFrameMinutes));
        return new JsonObject
        {
            ["date"] = schedule.DateKey == "day1" ? "${scheduleDate}" : "${scheduleDate2}",
            ["devices"] = new JsonArray(new JsonObject { ["deviceId"] = "${deviceId}", ["timeFrames"] = frames }),
        };
    }

    private static JsonObject FrameNode(VerifyCampaignModel campaign, VerifyDailySchedule schedule, string label, string seed, int firstFrameMinutes)
    {
        JsonObject frame = new() { ["timeFrameId"] = $"${{tf{FrameOffset(label, firstFrameMinutes) + 1}}}", ["spotCount"] = schedule.PerFrame.GetValueOrDefault(label) };
        if (!campaign.Kind.Equals("Duration", StringComparison.OrdinalIgnoreCase)) return frame;
        VerifyDurationGroup? group = schedule.Groups.FirstOrDefault(item => item.FrameLabels.Contains(label, StringComparer.OrdinalIgnoreCase));
        if (group is null) return frame;
        frame["timeFrameGroupKey"] = StableGuid(seed, schedule.Groups.ToList().IndexOf(group) + 1);
        frame["timeFrameGroupOrder"] = group.FrameLabels.ToList().IndexOf(label) + 1;
        return frame;
    }

    private static JsonObject EvidenceStep(string name, bool expectedValid) => DatabaseStep(
        name,
        "SELECT COUNT(*) FROM public.campaign WHERE \"Name\"=@name",
        false,
        new Dictionary<string, string> { ["name"] = "${candidateName}" },
        expectedValid ? "1" : "0");

    private static JsonObject DatabaseStep(string name, string command, bool resultSet, Dictionary<string, string>? parameters, string? scalar = null)
    {
        JsonObject database = new() { ["connectionStringVariable"] = "BE_BOOKING_DB_CONNECTION", ["command"] = command };
        if (parameters is not null) database["parameters"] = JsonSerializer.SerializeToNode(parameters);
        return new JsonObject
        {
            ["name"] = name,
            ["request"] = new JsonObject { ["database"] = database },
            ["expect"] = new JsonObject { ["database"] = scalar is null ? new JsonObject { ["resultSet"] = resultSet } : new JsonObject { ["scalarEquals"] = scalar } },
        };
    }

    private static JsonObject Coverage(string verifyStep, string evidenceStep)
    {
        JsonObject Covered(params string[] names) => new() { ["status"] = "covered", ["evidenceSteps"] = JsonSerializer.SerializeToNode(names) };
        JsonObject NotApplicable(string reason) => new() { ["status"] = "notApplicable", ["reason"] = reason };
        return new JsonObject
        {
            ["sourceVerification"] = new JsonObject
            {
                ["files"] = new JsonArray("D:/works/projects/mtc/be-booking/tools/booking-studio/importer/extract_verify_v21.py", "D:/works/projects/mtc/be-booking/src/Web/Controllers/Campaigns/CampaignsController.cs"),
                ["verifiedAt"] = "2026-09-28T00:00:00+07:00",
                ["contractVersion"] = "verify-workbook-v2",
            },
            ["contract"] = Covered(verifyStep),
            ["validation"] = Covered("Chọn fixture động theo thời điểm chạy"),
            ["authentication"] = Covered(verifyStep),
            ["scope"] = NotApplicable("Dedicated authorization suites cover advertiser scope."),
            ["integrity"] = Covered(evidenceStep),
            ["lifecycle"] = NotApplicable("VERIFY cases do not approve or dispatch campaigns."),
            ["concurrency"] = NotApplicable("Concurrency is covered by dedicated suites."),
            ["reporting"] = Covered(verifyStep, evidenceStep),
        };
    }

    private static string StableGuid(string seed, int index)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{index}"));
        return new Guid(hash[..16]).ToString();
    }

    private static int FrameOffset(string label, int firstFrameMinutes) => (FrameMinutes(label) - firstFrameMinutes) / 8;

    private static int FrameMinutes(string label)
    {
        string[] values = label.Split(':');
        int hour = int.Parse(values[0], CultureInfo.InvariantCulture);
        int minute = int.Parse(values[1][..2], CultureInfo.InvariantCulture);
        return hour * 60 + minute;
    }
}
