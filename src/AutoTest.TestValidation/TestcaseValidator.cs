using System.Text.Json;
using AutoTest.Abstractions;

namespace AutoTest.TestValidation;

public sealed record ValidationIssue(string Location, string Message);

public static class TestcaseValidator
{
    private static readonly string[] TemporaryMarkers = [".working.json", ".bak.json", ".backup.json", ".tmp.json"];
    private static readonly HashSet<string> BuiltInVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "unique", "timestamp", "timestampMs", "nowIso", "pastStartIso", "futureStartIso", "futureEndIso",
        "futureDay1Iso", "futureDay4Iso", "futureDay5Iso", "futureDay6Iso",
        "futureDay8Iso", "futureDay9Iso", "futureDay10Iso", "futureDay15Iso"
    };

    public static IReadOnlyList<ValidationIssue> Validate(
        string testcaseDirectory,
        ProjectSpec project,
        IReadOnlyList<CaseSpec> cases,
        IReadOnlyCollection<ITestStepExecutor> executors,
        bool validateTemporaryFiles = true)
    {
        var issues = new List<ValidationIssue>();
        var selectedGroups = cases.Select(test => test.SourceGroup).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(testcaseDirectory, "*.json", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(testcaseDirectory, file);
            string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string group = segments.Length > 1 ? segments[0] : "_root";
            if (validateTemporaryFiles && selectedGroups.Contains(group) && TemporaryMarkers.Any(marker => file.EndsWith(marker, StringComparison.OrdinalIgnoreCase)))
                issues.Add(new(file, "File tạm kết thúc bằng .json sẽ bị runner thực thi. Hãy đổi sang đuôi không phải .json."));
        }

        foreach (IGrouping<string, CaseSpec> duplicate in cases.GroupBy(test => test.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            issues.Add(new(duplicate.Key, "ID testcase bị trùng."));

        foreach (CaseSpec test in cases)
        {
            if (test.Tags is null || test.Tags.Length == 0)
                issues.Add(new(test.Id, "Testcase phải có ít nhất một tag."));
            if (test.Tags?.Any(string.IsNullOrWhiteSpace) == true)
                issues.Add(new(test.Id, "Tag không được rỗng."));
            ValidateSteps(test.Id, test.Steps, test.Variables, executors, issues, cleanup: false);
            ValidateSteps(test.Id, test.Cleanup ?? [], test.Variables, executors, issues, cleanup: true);
        }
        return issues;
    }

    private static void ValidateSteps(string caseId, IReadOnlyList<StepSpec> steps,
        Dictionary<string, string>? caseVariables, IReadOnlyCollection<ITestStepExecutor> executors,
        List<ValidationIssue> issues, bool cleanup)
    {
        var variables = new HashSet<string>(BuiltInVariables, StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index <= 32; index++) variables.Add($"guid{index}");
        foreach (string name in caseVariables?.Keys ?? Enumerable.Empty<string>()) variables.Add(name);

        foreach (StepSpec step in steps)
        {
            string location = $"{caseId} / {step.Name}";
            if (step.ConcurrentRequests is { Count: > 0 })
            {
                if (step.Request is not null || step.Save is not null || step.Retry is not null || step.ParallelRequests is not null)
                    issues.Add(new(location, "concurrentRequests xung đột với request/save/retry/parallelRequests cấp bước."));
                foreach (ConcurrentRequestSpec concurrent in step.ConcurrentRequests)
                {
                    StepSpec child = new(concurrent.Name, concurrent.Auth, concurrent.AuthToken,
                        concurrent.Request, concurrent.Expect, null, null, null, null);
                    var inheritedVariables = variables.ToDictionary(
                        variable => variable,
                        _ => string.Empty,
                        StringComparer.OrdinalIgnoreCase);
                    ValidateSteps(caseId, [child], inheritedVariables, executors, issues, cleanup);
                }
                continue;
            }
            if (step.ConcurrentRequests is { Count: 0 })
                issues.Add(new(location, "concurrentRequests không được để rỗng."));
            if (step.Request is null) { issues.Add(new(location, "Thiếu request.")); continue; }
            int transportCount = (step.Request.Database is null ? 0 : 1) + (step.Request.Mqtt is null ? 0 : 1) + (step.Request.HttpStub is null ? 0 : 1) + (step.Request.Method is null && step.Request.Path is null ? 0 : 1);
            if (transportCount != 1) issues.Add(new(location, "Mỗi bước phải khai báo đúng một transport HTTP, PostgreSQL hoặc MQTT."));
            int executorCount = executors.Count(executor => executor.CanExecute(step));
            if (executorCount != 1) issues.Add(new(location, $"Bước phải khớp đúng một executor, thực tế {executorCount}."));
            if (step.Request.Body is not null && step.Request.Form is not null) issues.Add(new(location, "Không được gửi đồng thời body JSON và form."));
            if (!cleanup && step.Expect is null) issues.Add(new(location, "Thiếu expect."));
            if (step.Expect is { Status: not null, StatusOneOf: { Length: > 0 } }) issues.Add(new(location, "Chỉ khai báo một trong status hoặc statusOneOf."));
            if (step.Expect?.Database is { ScalarEquals: not null, ResultSet: true })
                issues.Add(new(location, "Chỉ khai báo một trong database.scalarEquals hoặc database.resultSet."));
            if (step.Request.HttpStub is { } stub)
            {
                if (string.IsNullOrWhiteSpace(stub.Action)) issues.Add(new(location, "HttpStub action không được rỗng."));
                if (stub.Action.Equals("configure", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(stub.Path))
                    issues.Add(new(location, "HttpStub configure bắt buộc có path."));
                if (stub.Status is < 100 or > 599) issues.Add(new(location, "HttpStub status phải từ 100 đến 599."));
                if (stub.DelayMs is < 0 or > 300000) issues.Add(new(location, "HttpStub delayMs phải từ 0 đến 300000."));
                if (stub.TimeoutMs is < 1 or > 300000) issues.Add(new(location, "HttpStub timeoutMs phải từ 1 đến 300000."));
            }
            if (step.ParallelRequests is < 1 or > 1000) issues.Add(new(location, "parallelRequests phải từ 1 đến 1000."));
            if (step.ParallelRequests is > 1 && step.Save is not null) issues.Add(new(location, "Không thể vừa chạy song song vừa save biến."));
            foreach (string template in EnumerateTemplates(step))
                foreach (string variable in Templates.Variables(template))
                    if (!variable.StartsWith("env:", StringComparison.OrdinalIgnoreCase) && !variables.Contains(variable))
                        issues.Add(new(location, $"Biến ${{{variable}}} được dùng trước khi được khai báo hoặc save."));
            foreach (string saved in step.Save?.Keys ?? Enumerable.Empty<string>()) variables.Add(saved);
        }
    }

    private static IEnumerable<string> EnumerateTemplates(StepSpec step)
    {
        if (step.AuthToken is not null) yield return step.AuthToken;
        if (step.Request?.Path is not null) yield return step.Request.Path;
        if (step.Request?.Body is { } body) yield return body.GetRawText();
        foreach (string value in step.Request?.Form?.Values ?? Enumerable.Empty<string>()) yield return value;
        foreach (string value in step.Request?.Headers?.Values ?? Enumerable.Empty<string>()) yield return value;
        if (step.Request?.Mqtt is { } mqtt)
        {
            foreach (string? value in new[] { mqtt.Topic, mqtt.Payload, mqtt.Username, mqtt.Password, mqtt.ClientId, mqtt.Will?.Topic, mqtt.Will?.Payload })
                if (value is not null) yield return value;
        }
        foreach (string value in step.Request?.Database?.Parameters?.Values ?? Enumerable.Empty<string>()) yield return value;
        if (step.Request?.HttpStub is { } stub)
        {
            if (stub.Method is not null) yield return stub.Method;
            if (stub.Path is not null) yield return stub.Path;
            if (stub.Response is { } response) yield return response.GetRawText();
            foreach (string value in stub.ResponseHeaders?.Values ?? Enumerable.Empty<string>()) yield return value;
        }
        foreach (AssertionSpec assertion in step.Expect?.Json?.Values ?? Enumerable.Empty<AssertionSpec>())
        {
            if (assertion.ExpectedValue is { } expected) yield return expected.GetRawText();
            if (assertion.NotEquals is { } notExpected) yield return notExpected.GetRawText();
            if (assertion.Contains is not null) yield return assertion.Contains;
            if (assertion.Matches is not null) yield return assertion.Matches;
            foreach (JsonElement value in assertion.OneOf ?? []) yield return value.GetRawText();
        }
    }
}
