using System.Globalization;
using System.Text.RegularExpressions;
using AutoTest.Workbooks;

namespace AutoTest.ProjectAdapters.BeBooking;

public sealed partial class VerifyWorkbookParser
{
    public IReadOnlyList<VerifyCaseModel> Parse(WorkbookDocument workbook)
    {
        WorkbookSheet sheet = workbook.Sheets.FirstOrDefault(item => item.Name.Equals("VERIFY", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The VERIFY sheet was not found.");
        List<(int Row, int Number, string Title)> starts = sheet.Cells.Values
            .Where(cell => !string.IsNullOrWhiteSpace(Effective(sheet, cell.Reference)))
            .Select(cell => (cell.Row, Match: TitlePattern().Match(Effective(sheet, cell.Reference)!), Title: Effective(sheet, cell.Reference)!))
            .Where(item => item.Match.Success)
            .Select(item => (item.Row, int.Parse(item.Match.Groups[1].Value, CultureInfo.InvariantCulture), item.Title))
            .GroupBy(item => item.Row)
            .Select(group => group.First())
            .OrderBy(item => item.Row)
            .ToList();

        List<VerifyCaseModel> result = [];
        for (int index = 0; index < starts.Count; index++)
        {
            (int row, int number, string title) = starts[index];
            int endRow = index + 1 < starts.Count ? starts[index + 1].Row - 1 : sheet.Cells.Values.Max(cell => cell.Row);
            List<VerifyRow> rows = Enumerable.Range(row, Math.Max(0, endRow - row + 1)).Select(currentRow => new VerifyRow(currentRow, sheet)).ToList();
            IReadOnlyList<VerifyCampaignModel> dayOne = ParseDay(rows, 0, "day1");
            IReadOnlyList<VerifyCampaignModel> dayTwo = ParseDay(rows, 10, "day2");
            if (dayOne.Count == 0 || dayTwo.Count == 0)
                continue;

            VerifyCampaignModel candidateOne = Candidate(dayOne);
            VerifyCampaignModel candidateTwo = Candidate(dayTwo);
            bool reverse = rows.Any(item => item.Contains("ngày 2 sang ngày 1"));
            VerifyCampaignModel source = reverse ? candidateTwo : candidateOne;
            VerifyCampaignModel target = reverse ? candidateOne : candidateTwo;
            bool valid = IsValid(target, source);
            string? testerResult = rows.SelectMany(item => item.Values).FirstOrDefault(value => value.StartsWith("BUG:", StringComparison.OrdinalIgnoreCase) || value.Equals("PASS", StringComparison.OrdinalIgnoreCase));
            result.Add(new($"verify/{number}", title.Replace("VERIFY - ", "", StringComparison.OrdinalIgnoreCase), Merge(dayOne, dayTwo, candidateOne, candidateTwo), new(valid, testerResult, target.Kind.Equals("Spot", StringComparison.OrdinalIgnoreCase)), reverse ? "day2-to-day1" : "day1-to-day2", row));
        }
        return result;
    }

    private static IReadOnlyList<VerifyCampaignModel> Merge(IReadOnlyList<VerifyCampaignModel> first, IReadOnlyList<VerifyCampaignModel> second, VerifyCampaignModel candidateOne, VerifyCampaignModel candidateTwo)
    {
        List<VerifyCampaignModel> merged = [];
        foreach (VerifyCampaignModel campaign in first.Concat(second))
        {
            bool candidate = ReferenceEquals(campaign, candidateOne) || ReferenceEquals(campaign, candidateTwo);
            string key = candidate ? $"candidate:{campaign.Kind}" : $"{campaign.Name.Trim().ToLowerInvariant()}:{campaign.Kind}";
            if (merged.Any(item => (ReferenceEquals(item, candidateOne) || ReferenceEquals(item, candidateTwo)) && candidate && item.Kind.Equals(campaign.Kind, StringComparison.OrdinalIgnoreCase)))
                continue;
            VerifyCampaignModel? existing = merged.FirstOrDefault(item => item.Name.Trim().Equals(campaign.Name.Trim(), StringComparison.OrdinalIgnoreCase) && item.Kind.Equals(campaign.Kind, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                merged.Add(campaign);
            else
                merged[merged.IndexOf(existing)] = existing with { DailySchedules = existing.DailySchedules.Concat(campaign.DailySchedules).ToArray() };
        }
        return merged;
    }

    private static IReadOnlyList<VerifyCampaignModel> ParseDay(IReadOnlyList<VerifyRow> rows, int offset, string dateKey)
    {
        List<MutableCampaign> campaigns = [];
        MutableCampaign? current = null;
        foreach (VerifyRow row in rows)
        {
            string kind = row.Value(2 + offset).Trim();
            if (kind is "Spot" or "Duration")
            {
                current = new(row.Value(1 + offset).Trim(), kind);
                campaigns.Add(current);
            }
            if (current is null) continue;
            string frame = row.Value(3 + offset).Trim();
            if (!TryFrame(frame, out _)) continue;
            int? maximum = Number(row.Value(6 + offset));
            int? requested = Number(row.Value(7 + offset));
            int? allocation = Number(row.Value(8 + offset));
            if (requested.HasValue || (current.Kind == "Spot" && maximum.HasValue && current.Groups.Count > 0 && current.Groups[^1].Frames.Count > 0))
                current.Groups.Add(new MutableGroup(requested ?? 0, maximum));
            if (current.Groups.Count == 0) current.Groups.Add(new MutableGroup(0, maximum));
            if (maximum.HasValue && current.Groups[^1].Maximum is null) current.Groups[^1].Maximum = maximum;
            current.Frames.Add(frame);
            current.Groups[^1].Frames.Add(frame);
            if (allocation.HasValue)
            {
                current.PerFrame[frame] = allocation.Value;
                current.Groups[^1].PerFrame[frame] = allocation.Value;
            }
            else if (row.Value(8 + offset).Trim().Equals("FAIL", StringComparison.OrdinalIgnoreCase)) current.HasFail = true;
        }
        return campaigns.Select((campaign, index) => campaign.ToModel(dateKey, index + 1)).ToArray();
    }

    private static VerifyCampaignModel Candidate(IReadOnlyList<VerifyCampaignModel> campaigns) => campaigns.LastOrDefault(item => item.Name.Contains("TEST", StringComparison.OrdinalIgnoreCase)) ?? campaigns[^1];
    private static bool IsValid(VerifyCampaignModel campaign, VerifyCampaignModel source)
    {
        if (campaign.Kind.Equals("Spot", StringComparison.OrdinalIgnoreCase) && campaign.DailySchedules.All(item => item.PerFrame.Count == 0))
        {
            for (int dayIndex = 0; dayIndex < Math.Min(campaign.DailySchedules.Count, source.DailySchedules.Count); dayIndex++)
            {
                IReadOnlyList<VerifyDurationGroup> sourceGroups = source.DailySchedules[dayIndex].Groups;
                IReadOnlyList<VerifyDurationGroup> targetGroups = campaign.DailySchedules[dayIndex].Groups;
                for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
                {
                    int? maximum = groupIndex < targetGroups.Count ? targetGroups[groupIndex].Maximum : null;
                    if (maximum.HasValue && sourceGroups[groupIndex].Requested > maximum.Value) return false;
                }
            }
            return true;
        }

        return !campaign.DailySchedules.SelectMany(item => item.Groups).Any(group => group.Maximum.HasValue && group.Requested > group.Maximum.Value)
            && (campaign.Kind.Equals("Spot", StringComparison.OrdinalIgnoreCase) || campaign.DailySchedules.All(item => item.PerFrame.Values.Sum() == item.Requested));
    }
    private static string Effective(WorkbookSheet sheet, string reference) => OpenXmlWorkbookReader.EffectiveValue(sheet, reference) ?? string.Empty;
    private static int? Number(string value) => int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int number) ? number : null;
    private static bool TryFrame(string value, out int index) { Match match = FramePattern().Match(value); index = match.Success ? ((int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)) - 360) / 8 : -1; return match.Success; }
    [GeneratedRegex(@"^VERIFY[ ]*-[ ]*Case[ ]+([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex TitlePattern();
    [GeneratedRegex(@"^([0-9]{1,2}):([0-9]{2})", RegexOptions.CultureInvariant)] private static partial Regex FramePattern();

    private sealed class MutableCampaign(string name, string kind)
    {
        public string Name { get; } = name;
        public string Kind { get; } = kind;
        public List<string> Frames { get; } = [];
        public Dictionary<string, int> PerFrame { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MutableGroup> Groups { get; } = [];
        public bool HasFail { get; set; }
        public VerifyCampaignModel ToModel(string dateKey, int order) => new(Name, Kind, order, [new(dateKey, Groups.Sum(group => group.Requested) == 0 ? PerFrame.Values.Sum() : Groups.Sum(group => group.Requested), Frames, PerFrame, Groups.Select(group => group.ToModel()).ToArray())]);
    }
    private sealed class MutableGroup(int requested, int? maximum)
    {
        public int Requested { get; set; } = requested;
        public int? Maximum { get; set; } = maximum;
        public List<string> Frames { get; } = [];
        public Dictionary<string, int> PerFrame { get; } = new(StringComparer.OrdinalIgnoreCase);
        public VerifyDurationGroup ToModel() => new(Requested, Maximum, Frames, PerFrame);
    }
    private sealed class VerifyRow(int row, WorkbookSheet sheet)
    {
        public int Row { get; } = row;
        public string Value(int column) => sheet.Cells.TryGetValue(CellReference(column, Row), out WorkbookCell? cell) ? cell.Value ?? string.Empty : string.Empty;
        public IEnumerable<string> Values => Enumerable.Range(1, 26).Select(Value);
        public bool Contains(string text) => Values.Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase));
        private static string CellReference(int column, int row) { string result = string.Empty; for (int value = column; value > 0; value = (value - 1) / 26) result = (char)('A' + (value - 1) % 26) + result; return result + row.ToString(CultureInfo.InvariantCulture); }
    }
}
