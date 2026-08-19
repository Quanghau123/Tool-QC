using AutoTest.Abstractions;
using AutoTest.Core;
using AutoTest.Http;
using AutoTest.Mqtt;
using AutoTest.PostgreSql;
using AutoTest.TestValidation;

Environment.SetEnvironmentVariable("FRAMEWORK_TEST_URL", "http://localhost");
var environment = EnvironmentStore.Load(Path.GetTempFileName());
var variables = new Dictionary<string, string> { ["id"] = "42" };
Equal("items/42", Templates.Resolve("items/${id}", variables, environment), "template variable");

var registry = new StepExecutorRegistry(new ITestStepExecutor[]
{
    new HttpStepExecutor(
        new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
        environment),
    new PostgreSqlStepExecutor(),
    new MqttStepExecutor(environment),
});
Equal("http", registry.Resolve(Step("GET", "/health", null, null)).Name, "HTTP executor");
Equal("postgresql", registry.Resolve(Step(null, null, new("SELECT 1", null), null)).Name, "PostgreSQL executor");
Equal("mqtt", registry.Resolve(Step(null, null, null, new("connect", null, null, null, null, null, null, null, null, null))).Name, "MQTT executor");

string fixtureRoot = Path.Combine(Path.GetTempPath(), $"autotest-{Guid.NewGuid():N}");
string eventsDirectory = Path.Combine(fixtureRoot, "events");
Directory.CreateDirectory(eventsDirectory);
string suitePath = Path.Combine(eventsDirectory, "sample.json");
File.WriteAllText(suitePath, """
{"project":"framework-test","cases":[{"id":"sample.case","name":"Kịch bản mẫu","tags":["smoke"],"destructive":false,"steps":[{"name":"Đọc dữ liệu mẫu","request":{"method":"GET","path":"/health"},"expect":{"status":200}}]}]}
""");
CaseSpec loadedCase = SpecLoader.Cases(fixtureRoot, "framework-test").Single();
Equal("events", loadedCase.SourceGroup, "testcase source group");
IReadOnlyList<ValidationIssue> validIssues = TestcaseValidator.Validate(fixtureRoot,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [loadedCase], registry.All);
Equal("0", validIssues.Count.ToString(), "valid testcase preflight");
File.WriteAllText(Path.Combine(eventsDirectory, "duplicate.working.json"), File.ReadAllText(suitePath));
IReadOnlyList<ValidationIssue> invalidIssues = TestcaseValidator.Validate(fixtureRoot,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [loadedCase, loadedCase], registry.All);
Equal("true", (invalidIssues.Any(issue => issue.Message.Contains("File tạm", StringComparison.Ordinal)) &&
    invalidIssues.Any(issue => issue.Message.Contains("bị trùng", StringComparison.Ordinal))).ToString().ToLowerInvariant(),
    "invalid testcase preflight");
Directory.Delete(fixtureRoot, true);

Console.WriteLine("Framework tests passed: 7");
return 0;

static StepSpec Step(string? method, string? path, DatabaseRequestSpec? database, MqttRequestSpec? mqtt)
    => new("test", null, null, new(method, path, null, null, null, mqtt, database), null, null, null, null, null);
static void Equal(string expected, string actual, string name)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
}
