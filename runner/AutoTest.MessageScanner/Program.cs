using AutoTest.MessageScanner;

string? Argument(string key)
{
    int index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Cách dùng: dotnet run --project runner/AutoTest.MessageScanner -- --source <đường-dẫn-source> [--output <file.xlsx>]");
    return 0;
}

string? source = Argument("--source");
if (string.IsNullOrWhiteSpace(source))
{
    Console.Error.WriteLine("Thiếu --source <đường-dẫn-source>.");
    return 2;
}

try
{
    string sourcePath = Path.GetFullPath(source);
    string output = Argument("--output") ?? Path.Combine(
        Directory.GetCurrentDirectory(),
        "message-results",
        $"{Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}-messages-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");

    var result = new SourceMessageScanner().Scan(sourcePath);
    string report = new MessageWorkbookWriter().Write(output, result);
    Console.WriteLine($"Source: {result.SourceDirectory}");
    Console.WriteLine($"Đã quét: {result.ScannedFileCount} file");
    Console.WriteLine($"Messages duy nhất: {result.Messages.Count}");
    Console.WriteLine($"Modules: {result.Messages.Select(x => x.Module).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"File bỏ qua: {result.SkippedFiles.Count}");
    Console.WriteLine($"Báo cáo Excel: {report}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Quét messages thất bại: {exception.Message}");
    return 1;
}
