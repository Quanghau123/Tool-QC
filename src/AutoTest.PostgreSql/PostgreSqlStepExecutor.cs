using AutoTest.Abstractions;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
namespace AutoTest.PostgreSql;

public sealed class PostgreSqlStepExecutor : ITestStepExecutor
{
    public string Name => "postgresql";
    public bool CanExecute(StepSpec step) => step.Request?.Database is not null;
    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var watch=Stopwatch.StartNew();StepSpec step=context.Step;DatabaseRequestSpec database=step.Request!.Database!;string commandText=database.Command;DatabaseExpectSpec? expectation=context.Assertions?step.Expect?.Database:null;string expected=expectation?.ScalarEquals is { } scalar?$"Giá trị PostgreSQL = {Templates.Resolve(scalar,context.Variables,context.Environment)}":context.Assertions?"Lệnh PostgreSQL thực thi thành công":"Bước dọn dữ liệu PostgreSQL";
        try{var values=(database.Parameters??[]).ToDictionary(x=>x.Key,x=>Templates.Resolve(x.Value,context.Variables,context.Environment),StringComparer.OrdinalIgnoreCase);var order=new List<string>();commandText=Regex.Replace(commandText,@"@([A-Za-z_][A-Za-z0-9_]*)",m=>{string name=m.Groups[1].Value;if(!values.ContainsKey(name))throw new InvalidDataException($"Thiếu tham số PostgreSQL: {name}");int index=order.FindIndex(x=>x.Equals(name,StringComparison.OrdinalIgnoreCase));if(index<0){order.Add(name);index=order.Count-1;}return $"${index+1}";});await using var connection=new NpgsqlConnection(context.Environment.Require("DB_CONNECTION_STRING"));await connection.OpenAsync(cancellationToken);await using var command=new NpgsqlCommand(commandText,connection);foreach(string name in order)command.Parameters.Add(new NpgsqlParameter{Value=values[name]});string actual;if(expectation?.ScalarEquals is { } template){string wanted=Templates.Resolve(template,context.Variables,context.Environment);object? value=await command.ExecuteScalarAsync(cancellationToken);actual=value is null or DBNull?"null":Convert.ToString(value,CultureInfo.InvariantCulture)??"";if(actual!=wanted)throw new InvalidOperationException($"{step.Name}: mong đợi giá trị PostgreSQL '{wanted}', thực tế '{actual}'.");actual=$"Giá trị thực tế: {actual}";}else actual=$"Số dòng ảnh hưởng: {await command.ExecuteNonQueryAsync(cancellationToken)}";return new(step.Name,context.Cleanup,true,"POSTGRESQL","database",null,expected,null,actual,watch.Elapsed,null);}catch(Exception ex){return new(step.Name,context.Cleanup,false,"POSTGRESQL","database",null,expected,null,null,watch.Elapsed,context.Redact?.Invoke(ex.Message)??ex.Message);}
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}


