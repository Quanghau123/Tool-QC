namespace AutoTest.Workbooks;

public sealed record WorkbookImportOptions(
    long MaximumFileBytes = 20 * 1024 * 1024,
    int MaximumArchiveEntries = 5000,
    long MaximumUncompressedBytes = 200 * 1024 * 1024,
    double MaximumCompressionRatio = 200,
    int MaximumCellsPerSheet = 1000000);

public sealed record WorkbookCell(
    string Reference,
    int Row,
    int Column,
    string? Value,
    string? Formula,
    string? DataType,
    uint StyleIndex);

public sealed record WorkbookMerge(string Reference, string StartCell, string EndCell);

public sealed record WorkbookSheet(
    string Name,
    bool Hidden,
    IReadOnlyDictionary<string, WorkbookCell> Cells,
    IReadOnlyList<WorkbookMerge> Merges,
    int FormulaCount);

public sealed record WorkbookDocument(
    string FileName,
    string Sha256,
    long FileSize,
    IReadOnlyList<WorkbookSheet> Sheets);

public sealed record WorkbookDiagnostic(string Code, string Message, string? Sheet = null, string? Range = null);

public sealed record WorkbookCaseDescriptor(
    string Id,
    string Sheet,
    string Family,
    string Number,
    string Title,
    string SourceRange,
    bool Supported,
    string? BlockedReason);

public sealed record WorkbookAnalysis(
    string Project,
    string Adapter,
    string FileName,
    string Sha256,
    IReadOnlyList<WorkbookCaseDescriptor> Cases,
    IReadOnlyList<WorkbookDiagnostic> Diagnostics);

public interface IWorkbookProjectAdapter
{
    string Project { get; }
    string Name { get; }
    WorkbookAnalysis Analyze(WorkbookDocument workbook);
    string Generate(WorkbookDocument workbook, WorkbookCaseDescriptor descriptor, string repositoryRoot);
}
