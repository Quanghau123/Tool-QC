using AutoTest.Abstractions;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AutoTest.PostgreSql;

// Transport PostgreSQL: chạy fixture/assertion và đưa result set vào báo cáo.
public sealed class PostgreSqlStepExecutor : ITestStepExecutor
{
    public string Name => "postgresql";
    public bool CanExecute(StepSpec step) => step.Request?.Database is not null;
    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)

    {
        var watch = Stopwatch.StartNew();
        StepSpec step = context.Step;
        DatabaseRequestSpec database = step.Request!.Database!;
        string commandText = database.Command;
        string? requestPayload = null;
        DatabaseExpectSpec? expectation = context.Assertions ? step.Expect?.Database : null;
        string expected = expectation?.ScalarEquals is { } scalar
            ? $"Giá trị PostgreSQL = {Templates.Resolve(scalar, context.Variables, context.Environment)}"
            : expectation?.ResultSet == true ? "Trả về tập kết quả PostgreSQL" : "Lệnh PostgreSQL thực thi thành công";
        string? actual = null;
        try
        {
            var values = (database.Parameters ?? []).ToDictionary(x => x.Key, x => Templates.Resolve(x.Value, context.Variables, context.Environment), StringComparer.OrdinalIgnoreCase);
            requestPayload = FormatRequestPayload(database.Command, values, context.Redact);
            var order = new List<string>();
            commandText = Regex.Replace(commandText, @"@([A-Za-z_][A-Za-z0-9_]*)", match =>
            {
                string name = match.Groups[1].Value;
                if (!values.ContainsKey(name)) throw new InvalidDataException($"Thiếu tham số PostgreSQL: {name}");
                int index = order.FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0) { order.Add(name); index = order.Count - 1; }
                return $"${index + 1}";
            });
            string connectionVariable = string.IsNullOrWhiteSpace(database.ConnectionStringVariable) ? "DB_CONNECTION_STRING" : Templates.Resolve(database.ConnectionStringVariable, context.Variables, context.Environment);
            await using var connection = new NpgsqlConnection(context.Environment.Require(connectionVariable));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(commandText, connection);
            foreach (string name in order) command.Parameters.Add(new NpgsqlParameter { Value = values[name] });
            if (expectation?.ScalarEquals is { } template)
            {
                string wanted = Templates.Resolve(template, context.Variables, context.Environment);
                object? value = await command.ExecuteScalarAsync(cancellationToken);
                string scalarValue = value is null or DBNull ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                actual = $"Giá trị thực tế: {scalarValue}";
                if (scalarValue != wanted) throw new InvalidOperationException($"{step.Name}: mong đợi giá trị PostgreSQL '{wanted}', thực tế '{scalarValue}'.");
            }
            else if (expectation?.ResultSet == true || step.Save is not null)
            {
                actual = await ReadResultSetAsync(command, cancellationToken);
                Save(step, context, actual);
                AssertJson(context, actual);
            }
            else actual = $"Số dòng ảnh hưởng: {await command.ExecuteNonQueryAsync(cancellationToken)}";
            return new(step.Name, context.Cleanup, true, "POSTGRESQL", connectionVariable, requestPayload, expected, null, actual, watch.Elapsed, null);
        }
        catch (Exception ex)
        {
            return new(step.Name, context.Cleanup, false, "POSTGRESQL", "database", requestPayload, expected, null, actual, watch.Elapsed, context.Redact?.Invoke(ex.Message) ?? ex.Message);
        }
    }


    private static string FormatRequestPayload(string command, IReadOnlyDictionary<string, string> parameters, Func<string, string>? redact)
    {
        string formattedCommand = FormatSql(command);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("SQL:");
        builder.AppendLine(redact?.Invoke(formattedCommand) ?? formattedCommand);
        builder.AppendLine();
        builder.AppendLine("Parameters:");
        if (parameters.Count == 0)
        {
            builder.Append("(không có tham số)");
            return builder.ToString();
        }
        foreach ((string name, string value) in parameters)
            builder.AppendLine($"- {name}: {redact?.Invoke(value) ?? value}");
        return builder.ToString().TrimEnd();
    }

    private static string FormatSql(string sql)
    {
        string result = sql.Replace("\\u0022", "\"", StringComparison.Ordinal)
            .Replace("\\u003E", ">", StringComparison.Ordinal)
            .Replace("\\u003C", "<", StringComparison.Ordinal)
            .Replace("\\u0026", "&", StringComparison.Ordinal);
        result = Regex.Replace(result, @"\\s+", " ").Trim();
        string[] keywords = ["WITH", "SELECT", "FROM", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN", "JOIN", "WHERE", "AND", "OR", "GROUP BY", "ORDER BY", "HAVING", "LIMIT", "OFFSET", "UNION ALL", "CASE", "WHEN", "THEN", "ELSE", "END"];
        foreach (string keyword in keywords)
            result = Regex.Replace(result, $@"\s+({Regex.Escape(keyword)})\s+", $"\n$1 ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s+(AS)\s+", " AS ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\n{2,}", "\n").Trim();
        return result;
    }

    private static async Task<string> ReadResultSetAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[reader.GetName(index)] = NormalizeValue(await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index));
            rows.Add(row);
        }

        return JsonSerializer.Serialize(new { rowCount = rows.Count, rows }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null or DBNull) return null;
        if (value is JsonDocument document) return document.RootElement.Clone();
        if (value is JsonElement element) return element.Clone();
        if (value is string text && (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')))
        {
            try { return JsonSerializer.Deserialize<JsonElement>(text); }
            catch (JsonException) { }
        }
        return value;
    }

    private static void Save(StepSpec step, StepExecutionContext context, string resultSet)
    {
        if (step.Save is null) return;
        using JsonDocument document = JsonDocument.Parse(resultSet);
        foreach ((string name, string path) in step.Save)
        {
            var selected = JsonPath.Select(document.RootElement, path);
            if (!selected.Found) throw new InvalidOperationException($"Không thể lưu biến {name} từ kết quả PostgreSQL.");
            context.Variables[name] = JsonPath.Text(selected.Value);
        }
    }

    private static void AssertJson(StepExecutionContext context, string resultSet)

    {
        if (!context.Assertions || context.Step.Expect?.Json is not { Count: > 0 } assertions) return;
        using JsonDocument document = JsonDocument.Parse(resultSet);
        foreach ((string path, AssertionSpec rule) in assertions)
        {
            (bool found, JsonElement value) = JsonPath.Select(document.RootElement, path);
            if (rule.Exists is bool exists)
            {
                if (found != exists) throw new InvalidOperationException($"Trạng thái tồn tại của trường PostgreSQL {path} không đúng.");
                if (!exists) continue;
            }
            if (!found) throw new InvalidOperationException($"Không tìm thấy trường PostgreSQL {path} trong result-set.");
            string actual = JsonPath.Text(value);
            string Resolve(JsonElement element) => Templates.Resolve(JsonPath.Text(element), context.Variables, context.Environment);
            if (rule.ExpectedValue is JsonElement expected && !JsonAssertionValues.Equal(value, expected, Resolve(expected)))
                throw new InvalidOperationException($"Giá trị PostgreSQL tại {path} không đúng. Thực tế: {actual}.");
            if (rule.NotEquals is JsonElement notExpected && JsonAssertionValues.Equal(value, notExpected, Resolve(notExpected)))
                throw new InvalidOperationException($"Giá trị PostgreSQL tại {path} không được bằng {actual}.");
            if (rule.Contains is string contains && !actual.Contains(Templates.Resolve(contains, context.Variables, context.Environment), StringComparison.Ordinal))
                throw new InvalidOperationException($"Giá trị PostgreSQL tại {path} không chứa nội dung mong đợi.");
        }
    }


    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}



