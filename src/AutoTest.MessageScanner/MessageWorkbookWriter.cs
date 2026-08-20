using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoTest.MessageScanner;

public sealed class MessageWorkbookWriter
{
    public string Write(string outputPath, MessageScanResult result)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (File.Exists(fullPath)) File.Delete(fullPath);

        var sheets = new List<SheetData>
        {
            new("Messages", result.Messages)
        };
        sheets.AddRange(result.Messages
            .GroupBy(message => message.Module, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SheetData(SafeSheetName(group.Key, sheets.Select(x => x.Name)), group.ToArray())));

        using var archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
        Write(archive, "[Content_Types].xml", ContentTypes(sheets.Count));
        Write(archive, "_rels/.rels", RootRelationships());
        Write(archive, "docProps/app.xml", AppProperties(sheets));
        Write(archive, "docProps/core.xml", CoreProperties());
        Write(archive, "xl/workbook.xml", Workbook(sheets));
        Write(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count));
        Write(archive, "xl/styles.xml", Styles());
        for (int index = 0; index < sheets.Count; index++)
            Write(archive, $"xl/worksheets/sheet{index + 1}.xml", Worksheet(sheets[index].Messages));
        return fullPath;
    }

    private static string Worksheet(IReadOnlyList<MessageEntry> messages)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><dimension ref=\"A1:D")
            .Append(messages.Count + 1).Append("\"/><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols><col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"65\" customWidth=\"1\"/><col min=\"3\" max=\"4\" width=\"45\" customWidth=\"1\"/></cols><sheetData>");
        Row(xml, 1, true, "STT", "Messages", "Mes tiếng Việt", "Mes tiếng Anh");
        for (int index = 0; index < messages.Count; index++)
        {
            MessageEntry message = messages[index];
            Row(xml, index + 2, false, (index + 1).ToString(), message.Key, message.Vietnamese ?? "", message.English ?? "");
        }
        xml.Append("</sheetData><autoFilter ref=\"A1:D").Append(messages.Count + 1).Append("\"/></worksheet>");
        return xml.ToString();
    }

    private static void Row(StringBuilder xml, int row, bool header, params string[] values)
    {
        xml.Append("<row r=\"").Append(row).Append("\">");
        for (int column = 0; column < values.Length; column++)
        {
            string reference = $"{(char)('A' + column)}{row}";
            xml.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\" s=\"")
                .Append(header ? 1 : 0).Append("\"><is><t xml:space=\"preserve\">")
                .Append(Escape(values[column])).Append("</t></is></c>");
        }
        xml.Append("</row>");
    }

    private static string ContentTypes(int sheetCount)
    {
        var overrides = new StringBuilder();
        for (int i = 1; i <= sheetCount; i++)
            overrides.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>{overrides}</Types>";
    }

    private static string Workbook(IReadOnlyList<SheetData> sheets) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>{string.Join("", sheets.Select((sheet, index) => $"<sheet name=\"{Escape(sheet.Name)}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>"))}</sheets></workbook>";

    private static string WorkbookRelationships(int sheetCount)
    {
        var relationships = new StringBuilder();
        for (int i = 1; i <= sheetCount; i++)
            relationships.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        relationships.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{relationships}</Relationships>";
    }

    private static string RootRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";
    private static string Styles() => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf></cellXfs></styleSheet>";
    private static string CoreProperties() => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:creator>Tool-QC</dc:creator><dcterms:created xsi:type=\"dcterms:W3CDTF\">{DateTime.UtcNow:O}</dcterms:created></cp:coreProperties>";
    private static string AppProperties(IReadOnlyList<SheetData> sheets) => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Tool-QC</Application><TitlesOfParts><vt:vector size=\"{sheets.Count}\" baseType=\"lpstr\">{string.Join("", sheets.Select(sheet => $"<vt:lpstr>{Escape(sheet.Name)}</vt:lpstr>"))}</vt:vector></TitlesOfParts></Properties>";

    private static string SafeSheetName(string requested, IEnumerable<string> existing)
    {
        string clean = RegexReplaceInvalidSheetCharacters(requested);
        if (string.IsNullOrWhiteSpace(clean)) clean = "Other";
        clean = clean.Length > 31 ? clean[..31] : clean;
        var used = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        string candidate = clean;
        for (int suffix = 2; used.Contains(candidate); suffix++)
        {
            string marker = $"-{suffix}";
            candidate = clean[..Math.Min(clean.Length, 31 - marker.Length)] + marker;
        }
        return candidate;
    }

    private static string RegexReplaceInvalidSheetCharacters(string value)
    {
        foreach (char invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' }) value = value.Replace(invalid, '-');
        return value.Trim('\'');
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private sealed record SheetData(string Name, IReadOnlyList<MessageEntry> Messages);
}
