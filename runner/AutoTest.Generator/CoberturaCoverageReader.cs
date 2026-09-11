using System.Globalization;
using System.Xml.Linq;

namespace AutoTest.Generator;

// Đọc Cobertura do Coverlet tạo để chỉ ra line và branch chưa được testcase chạm tới.
public static class CoberturaCoverageReader
{
    public static CoverageAnalysis Read(string reportPath)
    {
        string fullPath = Path.GetFullPath(reportPath);
        XDocument document = XDocument.Load(fullPath);
        XElement root = document.Root ?? throw new InvalidOperationException("Cobertura report has no root element.");
        int coveredLines = Int(root, "lines-covered");
        int totalLines = Int(root, "lines-valid");
        int coveredBranches = Int(root, "branches-covered");
        int totalBranches = Int(root, "branches-valid");
        List<CoverageGap> gaps = [];
        foreach (XElement type in document.Descendants("class"))
        {
            string source = type.Attribute("filename")?.Value ?? string.Empty;
            foreach (XElement method in type.Descendants("method"))
            {
                string methodName = method.Attribute("name")?.Value ?? "unknown";
                foreach (XElement line in method.Descendants("line"))
                {
                    int number = Parse(line.Attribute("number")?.Value);
                    int hits = Parse(line.Attribute("hits")?.Value);
                    if (hits == 0) gaps.Add(new(source, methodName, number, "line"));
                    if (string.Equals(line.Attribute("branch")?.Value, "true", StringComparison.OrdinalIgnoreCase) && BranchCoverage(line.Attribute("condition-coverage")?.Value) < 100) gaps.Add(new(source, methodName, number, "branch"));
                }
            }
        }
        return new(fullPath, coveredLines, totalLines, Rate(coveredLines, totalLines), coveredBranches, totalBranches, Rate(coveredBranches, totalBranches), gaps.Distinct().OrderBy(gap => gap.SourceFile).ThenBy(gap => gap.Line).ToArray());
    }

    private static int Int(XElement element, string attribute) => Parse(element.Attribute(attribute)?.Value);
    private static int Parse(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    private static int BranchCoverage(string? value) { if (string.IsNullOrWhiteSpace(value)) return 0; string percent = value.Split('%')[0]; return Parse(percent); }
    private static decimal Rate(int covered, int total) => total == 0 ? 0 : Math.Round((decimal)covered / total, 4);
}
