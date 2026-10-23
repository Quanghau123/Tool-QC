namespace AutoTest.UnitX;

public sealed record DecisionTable(
    string Name,
    IReadOnlyList<DecisionRule> Rules,
    IReadOnlyList<DecisionRow> Rows);

public sealed record DecisionRule(
    string Output,
    string Operator,
    IReadOnlyList<string> Inputs);

public sealed record DecisionRow(
    string Name,
    IReadOnlyDictionary<string, decimal> Values,
    decimal Expected);

public sealed record DecisionRowResult(
    string Name,
    decimal? Actual,
    decimal Expected,
    bool Passed,
    string? Error);

public sealed record DecisionTableResult(IReadOnlyList<DecisionRowResult> Rows)
{
    public int PassedCount => Rows.Count(row => row.Passed);
}
