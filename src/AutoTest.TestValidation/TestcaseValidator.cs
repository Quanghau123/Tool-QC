using System.Text.Json;
using AutoTest.Abstractions;

namespace AutoTest.TestValidation;

public sealed record ValidationIssue(string Location, string Message);

public static class TestcaseValidator
{
    private static readonly string[] TemporaryMarkers = [".working.json", ".bak.json", ".backup.json", ".tmp.json"];
    private static readonly HashSet<string> BuiltInVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "unique", "shortUniqueA", "shortUniqueB", "shortUniqueC", "timestamp", "timestampMs", "nowIso", "pastStartIso", "futureStartIso", "futureEndIso",
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
            if (test.SchemaVersion >= 2) ValidateCoverage(test, issues);
            ValidateSteps(test.Id, test.Steps, test.Variables, executors, issues, cleanup: false);
            ValidateSteps(test.Id, test.Cleanup ?? [], test.Variables, executors, issues, cleanup: true);
        }
        return issues;
    }

    private static void ValidateCoverage(CaseSpec test, List<ValidationIssue> issues)
    {
        string location = $"{test.Id} / coverage";
        if (test.Coverage is null) { issues.Add(new(location, "Thiếu coverage manifest bắt buộc.")); return; }
        if (test.Coverage.SourceVerification is not { Files: { Length: > 0 } } source)
            issues.Add(new(location, "sourceVerification.files phải liệt kê source backend đã đối chiếu."));
        else
        {
            if (source.Files.Any(string.IsNullOrWhiteSpace)) issues.Add(new(location, "sourceVerification.files không được chứa đường dẫn rỗng."));
            if (string.IsNullOrWhiteSpace(source.VerifiedAt) || !DateTimeOffset.TryParse(source.VerifiedAt, out _)) issues.Add(new(location, "sourceVerification.verifiedAt phải là ISO datetime hợp lệ."));
        }

        var items = new Dictionary<string, CoverageItemSpec?>
        {
            ["contract"] = test.Coverage.Contract, ["validation"] = test.Coverage.Validation,
            ["authentication"] = test.Coverage.Authentication, ["scope"] = test.Coverage.Scope,
            ["integrity"] = test.Coverage.Integrity, ["lifecycle"] = test.Coverage.Lifecycle,
            ["concurrency"] = test.Coverage.Concurrency, ["reporting"] = test.Coverage.Reporting,
        };
        var stepNames = test.Steps.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, CoverageItemSpec? item) in items)
        {
            if (item is null) { issues.Add(new(location, $"Thiếu coverage.{name}.")); continue; }
            string status = item.Status?.Trim().ToLowerInvariant() ?? "";
            if (status is not ("covered" or "notapplicable" or "blocked")) { issues.Add(new(location, $"coverage.{name}.status chỉ nhận covered, notApplicable hoặc blocked.")); continue; }
            if (status == "covered")
            {
                if (item.EvidenceSteps is not { Length: > 0 }) issues.Add(new(location, $"coverage.{name} covered nhưng thiếu evidenceSteps."));
                else foreach (string step in item.EvidenceSteps.Where(step => !stepNames.Contains(step))) issues.Add(new(location, $"coverage.{name} tham chiếu step không tồn tại: {step}"));
            }
            else if (string.IsNullOrWhiteSpace(item.Reason)) issues.Add(new(location, $"coverage.{name} {item.Status} nhưng thiếu reason."));
        }

        bool mutates = test.Destructive || test.Steps.Any(IsMutation);
        bool protectedApi = test.Steps.Any(step => step.Auth is not null || step.AuthToken is not null);
        bool scopedApi = test.Steps.Any(step => step.Request?.Path?.Contains("${eventId}", StringComparison.OrdinalIgnoreCase) == true);
        bool concurrency = test.Steps.Any(step => step.ParallelRequests is > 1 || step.ConcurrentRequests is { Count: > 1 });
        RequireCovered(test.Coverage.Contract, "contract", location, issues);
        RequireCovered(test.Coverage.Reporting, "reporting", location, issues);
        RequireCovered(test.Coverage.Validation, "validation", location, issues);
        if (mutates) RequireCovered(test.Coverage.Integrity, "integrity cho testcase mutation/destructive", location, issues);
        if (protectedApi) RequireCovered(test.Coverage.Authentication, "authentication cho API protected", location, issues);
        if (scopedApi) RequireCovered(test.Coverage.Scope, "scope cho route có eventId", location, issues);
        if (concurrency) RequireCovered(test.Coverage.Concurrency, "concurrency cho bước chạy đồng thời", location, issues);

        foreach (StepSpec step in test.Steps)
        {
            if (step.Request is null || step.ConcurrentRequests is { Count: > 0 }) continue;
            if (step.Request.Method is not null && step.Expect is { Status: not null } && step.Expect.Json is null && step.Expect.Database is null && step.Expect.Mqtt is null && step.Expect.HttpStub is null && step.Request.Path?.Contains("health", StringComparison.OrdinalIgnoreCase) != true)
                issues.Add(new($"{test.Id} / {step.Name}", "HTTP step chỉ assert status; phải assert payload hoặc database, trừ health endpoint."));
            if (IsMutation(step) && step.Expect?.Status is >= 400 && !HasLaterDatabaseEvidence(test.Steps, step))
                issues.Add(new($"{test.Id} / {step.Name}", "Mutation bị từ chối phải có bước database phía sau để chứng minh không đổi dữ liệu."));
        }
    }

    private static void RequireCovered(CoverageItemSpec? item, string name, string location, List<ValidationIssue> issues)
    { if (!string.Equals(item?.Status, "covered", StringComparison.OrdinalIgnoreCase)) issues.Add(new(location, $"Bắt buộc coverage {name} phải là covered.")); }
    private static bool IsMutation(StepSpec step) => step.Request?.Database is not null || step.Request?.Mqtt is not null || step.Request?.HttpStub is not null || step.Request?.Method?.ToUpperInvariant() is "POST" or "PUT" or "PATCH" or "DELETE";
    private static bool HasLaterDatabaseEvidence(IReadOnlyList<StepSpec> steps, StepSpec current)
    { int index=-1; for(int i=0;i<steps.Count;i++)if(ReferenceEquals(steps[i],current)){index=i;break;} return index>=0 && steps.Skip(index+1).Any(step=>step.Request?.Database is not null && step.Expect?.Database is not null); }

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
            if (step.Request.Body is not null && step.Request.RawBody is not null) issues.Add(new(location, "Không được gửi đồng thời body và rawBody."));
            if (step.Request.Form is not null && step.Request.RawBody is not null) issues.Add(new(location, "Không được gửi đồng thời form và rawBody."));
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
        if (step.Request?.RawBody is not null) yield return step.Request.RawBody;
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
