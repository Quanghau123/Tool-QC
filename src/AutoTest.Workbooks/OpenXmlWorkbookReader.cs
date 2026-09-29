using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AutoTest.Workbooks;

public sealed class OpenXmlWorkbookReader
{
    public WorkbookDocument Read(string path, WorkbookImportOptions? options = null)
    {
        WorkbookImportOptions settings = options ?? new();
        XlsxPackageGuard.Validate(path, settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using FileStream hashStream = File.OpenRead(path);
        string sha256 = Convert.ToHexString(SHA256.HashData(hashStream)).ToLowerInvariant();
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Workbook part is missing.");
        SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        List<WorkbookSheet> sheets = [];

        Workbook workbook = workbookPart.Workbook ?? throw new InvalidDataException("Workbook definition is missing.");
        Sheets sheetsElement = workbook.Sheets ?? throw new InvalidDataException("Workbook sheets are missing.");
        foreach (Sheet sheet in sheetsElement.Elements<Sheet>())
        {
            string relationshipId = sheet.Id?.Value ?? throw new InvalidDataException($"Sheet '{sheet.Name}' has no relationship id.");
            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(relationshipId);
            Worksheet worksheet = worksheetPart.Worksheet ?? throw new InvalidDataException($"Sheet '{sheet.Name}' has no worksheet definition.");
            Dictionary<string, WorkbookCell> cells = new(StringComparer.OrdinalIgnoreCase);
            int formulaCount = 0;
            foreach (Cell cell in worksheet.Descendants<Cell>())
            {
                if (cells.Count >= settings.MaximumCellsPerSheet)
                    throw new InvalidDataException($"Sheet '{sheet.Name}' exceeds the configured cell limit.");
                string reference = cell.CellReference?.Value ?? string.Empty;
                (int row, int column) = ParseReference(reference);
                string? value = ReadValue(cell, sharedStrings);
                string? formula = cell.CellFormula?.Text;
                if (formula is not null) formulaCount++;
                cells[reference] = new(reference, row, column, value, formula, cell.DataType?.Value.ToString(), cell.StyleIndex?.Value ?? 0);
            }

            List<WorkbookMerge> merges = worksheet.Elements<MergeCells>()
                .SelectMany(mergeCells => mergeCells.Elements<MergeCell>())
                .Select(merge => CreateMerge(merge.Reference?.Value ?? string.Empty))
                .ToList();
            bool hidden = sheet.State?.Value == SheetStateValues.Hidden || sheet.State?.Value == SheetStateValues.VeryHidden;
            sheets.Add(new(sheet.Name?.Value ?? string.Empty, hidden, cells, merges, formulaCount));
        }

        stopwatch.Stop();
        Console.Error.WriteLine($"[{DateTimeOffset.Now:O}] [WorkbookImport] file={Path.GetFileName(path)} sha={sha256[..12]} sheets={sheets.Count} durationMs={stopwatch.ElapsedMilliseconds}");
        return new(Path.GetFileName(path), sha256, new FileInfo(path).Length, sheets);
    }

    public static string? EffectiveValue(WorkbookSheet sheet, string reference)
    {
        if (sheet.Cells.TryGetValue(reference, out WorkbookCell? cell) && !string.IsNullOrWhiteSpace(cell.Value)) return cell.Value;
        WorkbookMerge? merge = sheet.Merges.FirstOrDefault(item => Contains(item.Reference, reference));
        return merge is null || !sheet.Cells.TryGetValue(merge.StartCell, out WorkbookCell? anchor) ? null : anchor.Value;
    }

    private static string? ReadValue(Cell cell, SharedStringTable? sharedStrings)
    {
        string? raw = cell.CellValue?.Text ?? cell.InlineString?.InnerText;
        if (raw is null) return null;
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            return sharedStrings?.ElementAtOrDefault(index)?.InnerText ?? raw;
        if (cell.DataType?.Value == CellValues.Boolean) return raw == "1" ? "true" : "false";
        return raw;
    }

    private static WorkbookMerge CreateMerge(string reference)
    {
        string[] cells = reference.Split(':', 2);
        return new(reference, cells[0], cells.Length == 2 ? cells[1] : cells[0]);
    }

    private static bool Contains(string range, string reference)
    {
        string[] ends = range.Split(':', 2);
        (int startRow, int startColumn) = ParseReference(ends[0]);
        (int endRow, int endColumn) = ParseReference(ends.Length == 2 ? ends[1] : ends[0]);
        (int row, int column) = ParseReference(reference);
        return row >= startRow && row <= endRow && column >= startColumn && column <= endColumn;
    }

    public static (int Row, int Column) ParseReference(string reference)
    {
        int column = 0;
        int index = 0;
        while (index < reference.Length && char.IsLetter(reference[index]))
        {
            column = (column * 26) + char.ToUpperInvariant(reference[index]) - 'A' + 1;
            index++;
        }
        return (int.TryParse(reference[index..], out int row) ? row : 0, column);
    }
}
