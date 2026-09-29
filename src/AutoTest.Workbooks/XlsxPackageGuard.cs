using System.IO.Compression;

namespace AutoTest.Workbooks;

public static class XlsxPackageGuard
{
    public static void Validate(string path, WorkbookImportOptions options)
    {
        FileInfo file = new(path);
        if (!file.Exists) throw new FileNotFoundException("Workbook file does not exist.", path);
        if (!file.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only .xlsx workbooks are supported.");
        if (file.Length > options.MaximumFileBytes)
            throw new InvalidDataException($"Workbook exceeds the {options.MaximumFileBytes} byte limit.");

        using FileStream stream = file.OpenRead();
        Span<byte> signature = stackalloc byte[4];
        if (stream.Read(signature) != signature.Length || signature[0] != 0x50 || signature[1] != 0x4B)
            throw new InvalidDataException("Workbook is not a valid ZIP-based XLSX package.");
        stream.Position = 0;
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count > options.MaximumArchiveEntries)
            throw new InvalidDataException("Workbook archive contains too many entries.");

        long totalUncompressed = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalUncompressed > options.MaximumUncompressedBytes)
                throw new InvalidDataException("Workbook expands beyond the configured byte limit.");
            if (entry.CompressedLength == 0 && entry.Length > 0)
                throw new InvalidDataException("Workbook contains an invalid compressed entry.");
            if (entry.CompressedLength > 0 && (double)entry.Length / entry.CompressedLength > options.MaximumCompressionRatio)
                throw new InvalidDataException("Workbook contains a suspicious compression ratio.");
        }
    }
}
