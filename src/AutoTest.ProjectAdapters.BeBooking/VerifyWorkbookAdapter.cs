using System.Text.RegularExpressions;
using AutoTest.Workbooks;

namespace AutoTest.ProjectAdapters.BeBooking;

public sealed partial class VerifyWorkbookAdapter : IWorkbookProjectAdapter
{
    public string Project => "be-booking";
    public string Name => "be-booking-campaign-verify-v1";

    public WorkbookAnalysis Analyze(WorkbookDocument workbook)
    {
        WorkbookSheet? sheet = workbook.Sheets.FirstOrDefault(item => item.Name.Equals("VERIFY", StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
            return new(Project, Name, workbook.FileName, workbook.Sha256, [], [new("TQC-XLSX-BE-001", "The VERIFY sheet was not found.")]);

        IReadOnlyDictionary<string, VerifyCaseModel> parsedCases = new VerifyWorkbookParser().Parse(workbook).ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        List<(int Row, string Title, string Number)> titles = sheet.Cells.Values
            .Where(cell => !string.IsNullOrWhiteSpace(cell.Value))
            .Select(cell => (cell.Row, Title: cell.Value!, Match: VerifyTitle().Match(cell.Value!)))
            .Where(item => item.Match.Success)
            .Select(item => (item.Row, item.Title, item.Match.Groups[1].Value))
            .GroupBy(item => item.Row)
            .Select(group => group.OrderBy(item => item.Title.Length).First())
            .OrderBy(item => item.Row)
            .ToList();

        List<WorkbookCaseDescriptor> cases = [];
        List<WorkbookDiagnostic> diagnostics = [];
        for (int index = 0; index < titles.Count; index++)
        {
            (int row, string title, string number) = titles[index];
            int endRow = index + 1 < titles.Count ? titles[index + 1].Row - 1 : sheet.Cells.Values.Max(cell => cell.Row);
            string id = $"verify/{number}";
            bool supported = parsedCases.ContainsKey(id);
            string? reason = supported ? null : "The case layout could not be mapped to a complete VERIFY model.";
            cases.Add(new(id, sheet.Name, "VERIFY", number, title, $"A{row}:Z{endRow}", supported, reason));
            if (!supported) diagnostics.Add(new("TQC-XLSX-BE-UNMAPPED", reason!, sheet.Name, $"A{row}:Z{endRow}"));
        }

        return new(Project, Name, workbook.FileName, workbook.Sha256, cases, diagnostics);
    }

    public string Generate(WorkbookDocument workbook, WorkbookCaseDescriptor descriptor, string repositoryRoot)
    {
        if (!descriptor.Supported)
            throw new InvalidOperationException($"Workbook case '{descriptor.Id}' is not supported by adapter '{Name}'.");
        VerifyCaseModel model = new VerifyWorkbookParser().Parse(workbook).Single(item => item.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase));
        return new VerifyTestcaseBuilder().Build(model, workbook);
    }

    [GeneratedRegex(@"^VERIFY\s*-\s*Case\s+(\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerifyTitle();
}
