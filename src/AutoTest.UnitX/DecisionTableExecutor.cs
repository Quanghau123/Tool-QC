namespace AutoTest.UnitX;

public static class DecisionTableExecutor
{
    private const int MaximumRules = 64;
    private const int MaximumRows = 1_000;

    public static DecisionTableResult Execute(DecisionTable table)
    {
        Validate(table);
        return new DecisionTableResult(table.Rows.Select(row => ExecuteRow(table.Rules, row)).ToArray());
    }

    private static DecisionRowResult ExecuteRow(IReadOnlyList<DecisionRule> rules, DecisionRow row)
    {
        try
        {
            Dictionary<string, decimal> values = new(row.Values, StringComparer.OrdinalIgnoreCase);
            foreach (DecisionRule rule in rules)
                values[rule.Output] = Apply(rule, values);
            decimal actual = values[rules[^1].Output];
            return new DecisionRowResult(row.Name, actual, row.Expected, actual == row.Expected, null);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new DecisionRowResult(row.Name, null, row.Expected, false, exception.Message);
        }
    }

    private static decimal Apply(DecisionRule rule, IReadOnlyDictionary<string, decimal> values)
    {
        decimal[] input = rule.Inputs.Select(name => values.TryGetValue(name, out decimal value)
            ? value
            : throw new InvalidDataException($"Không tìm thấy giá trị '{name}'.")).ToArray();
        return rule.Operator.ToLowerInvariant() switch
        {
            "add" => input[0] + input[1],
            "subtract" => input[0] - input[1],
            "multiply" => input[0] * input[1],
            "divide" when input[1] != 0 => input[0] / input[1],
            "round" => decimal.Round(input[0], decimal.ToInt32(input[1]), MidpointRounding.AwayFromZero),
            "ifgreaterthan" => input[0] > input[1] ? input[2] : input[3],
            _ => throw new InvalidDataException($"Operator UnitX không được hỗ trợ: {rule.Operator}.")
        };
    }

    private static void Validate(DecisionTable table)
    {
        if (string.IsNullOrWhiteSpace(table.Name) || table.Rules.Count is 0 or > MaximumRules || table.Rows.Count is 0 or > MaximumRows)
            throw new InvalidDataException("Bảng quyết định UnitX vượt giới hạn hoặc thiếu dữ liệu.");
        foreach (DecisionRule rule in table.Rules)
            if (string.IsNullOrWhiteSpace(rule.Output) || string.IsNullOrWhiteSpace(rule.Operator) || rule.Inputs.Count is 0 or > 4)
                throw new InvalidDataException("Rule UnitX không hợp lệ.");
    }
}
