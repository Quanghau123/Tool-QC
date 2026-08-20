using AutoTest.Abstractions;
using AutoTest.Core;
using AutoTest.Http;
using AutoTest.Mqtt;
using AutoTest.PostgreSql;
using AutoTest.HttpStub;
using AutoTest.TestValidation;
using System.Text.Json;
using AutoTest.Integration.Abstractions;
using AutoTest.Integration.Artifacts;
using AutoTest.Integration.Http;

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
    new HttpStubStepExecutor(environment),
});
Equal("http", registry.Resolve(Step("GET", "/health", null, null)).Name, "HTTP executor");
Equal("postgresql", registry.Resolve(Step(null, null, new("SELECT 1", null), null)).Name, "PostgreSQL executor");
Equal("mqtt", registry.Resolve(Step(null, null, null, new("connect", null, null, null, null, null, null, null, null, null))).Name, "MQTT executor");
var stubStep = new StepSpec("stub", null, null,
    new RequestSpec(null, null, null, null, null, null, null, new("start", null, null, null, null, null, null, null)),
    null, null, null, null, null);
Equal("http-stub", registry.Resolve(stubStep).Name, "HTTP stub executor");
await using (var stubExecutor = new HttpStubStepExecutor(environment))
{
    var stubVariables = new Dictionary<string, string>();
    StepRunResult started = await stubExecutor.ExecuteAsync(Context(
        new HttpStubRequestSpec("start", null, null, null, null, null, null, null), null), CancellationToken.None);
    using JsonDocument startedDocument = JsonDocument.Parse(started.ActualResponse!);
    string stubUrl = startedDocument.RootElement.GetProperty("baseUrl").GetString()!;
    StepRunResult configured = await stubExecutor.ExecuteAsync(Context(
        new HttpStubRequestSpec("configure", "POST", "/api/sync", 202,
            JsonDocument.Parse("""{"message":"accepted"}""").RootElement.Clone(), null, 0, null), null), CancellationToken.None);
    Equal("true", configured.Passed.ToString().ToLowerInvariant(), "HTTP stub configure");
    using var stubClient = new HttpClient();
    using HttpResponseMessage stubResponse = await stubClient.PostAsync($"{stubUrl}/api/sync",
        new StringContent("""{"activityId":"A-1"}""", System.Text.Encoding.UTF8, "application/json"));
    Equal("202", ((int)stubResponse.StatusCode).ToString(), "HTTP stub configured status");
    var inspectExpect = new HttpStubExpectSpec(1, "POST", "/api/sync",
        new Dictionary<string, AssertionSpec> { ["$.activityId"] = new() { ExpectedValue = JsonDocument.Parse("\"A-1\"").RootElement.Clone() } });
    StepRunResult inspected = await stubExecutor.ExecuteAsync(Context(
        new HttpStubRequestSpec("inspect", "POST", "/api/sync", null, null, null, null, 1000), inspectExpect), CancellationToken.None);
    Equal("true", inspected.Passed.ToString().ToLowerInvariant(), "HTTP stub capture and assertion");

    StepExecutionContext Context(HttpStubRequestSpec request, HttpStubExpectSpec? expect)
    {
        var step = new StepSpec("stub test", null, null,
            new RequestSpec(null, null, null, null, null, null, null, request),
            new ExpectSpec(null, null, null, null, null, null, expect), null, null, null, null);
        return new(step, new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null), environment, stubVariables, true, false);
    }
}

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

var concurrentCase = loadedCase with
{
    Variables = new Dictionary<string, string> { ["savedEarlier"] = "fixture" },
    Steps =
    [
        new StepSpec("Hai request dùng biến kế thừa", null, null, null, null, null, null, null,
        [
            new ConcurrentRequestSpec("Request A", null, null,
                new RequestSpec("POST", "/items/${savedEarlier}", null, null, null, null, null),
                new ExpectSpec(200, null, null, null, null, null)),
            new ConcurrentRequestSpec("Request B", null, null,
                new RequestSpec("POST", "/items/${savedEarlier}", null, null, null, null, null),
                new ExpectSpec(200, null, null, null, null, null))
        ])
    ]
};
IReadOnlyList<ValidationIssue> concurrentIssues = TestcaseValidator.Validate(fixtureRoot,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [concurrentCase], registry.All, validateTemporaryFiles: false);
Equal("0", concurrentIssues.Count.ToString(), "concurrent request inherits variables");
var resultSetExpectation = JsonSerializer.Deserialize<DatabaseExpectSpec>("""{"resultSet":true}""",
    new JsonSerializerOptions(JsonSerializerDefaults.Web));
Equal("true", (resultSetExpectation?.ResultSet == true).ToString().ToLowerInvariant(),
    "database result-set contract");

var sequenceRule = JsonSerializer.Deserialize<HttpIntegrationRule>("""{"method":"POST","path":"/sync","status":200,"response":null,"sequence":[{"status":500,"response":{"error":true}},{"status":200,"response":{"ok":true}}]}""",
    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
var integrationProfile = new IntegrationProfile("test", "http", "http://127.0.0.1:28999", [sequenceRule], true, 1024, 10);
HttpProfileValidator.Validate(integrationProfile);
Equal("A-1", JsonDocument.Parse(HttpTemplateRenderer.Render("\"${request:$.id}\"", JsonDocument.Parse("""{"id":"A-1"}""").RootElement)).RootElement.GetString()!, "HTTP integration template");
Equal("true", JsonRuleMatcher.Matches(new() { ["$.id"] = JsonDocument.Parse("\"A-1\"").RootElement.Clone() }, JsonDocument.Parse("""{"id":"A-1"}""").RootElement).ToString().ToLowerInvariant(), "HTTP integration JSON matcher");
string artifactRoot = Path.Combine(Path.GetTempPath(), $"integration-artifact-{Guid.NewGuid():N}");
var artifactWriter = new IntegrationArtifactWriter(artifactRoot);
var exchange = new CapturedExchange(1, Guid.NewGuid(), "POST", "/sync", new() { ["Authorization"] = "***" }, JsonDocument.Parse("""{"id":"A-1"}""").RootElement.Clone(), DateTimeOffset.Now, "sync", 200, JsonDocument.Parse("""{"ok":true}""").RootElement.Clone(), 12, 11, 3);
Equal("true", (await artifactWriter.ExchangeAsync(exchange)).ToString().ToLowerInvariant(), "integration artifact write");
await artifactWriter.IndexAsync([exchange]);
Equal("true", File.Exists(Path.Combine(artifactRoot, "requests", "000001.json")).ToString().ToLowerInvariant(), "integration request artifact exists");
Directory.Delete(artifactRoot, true);
Directory.Delete(fixtureRoot, true);

Console.WriteLine("Framework tests passed: 17");
return 0;

static StepSpec Step(string? method, string? path, DatabaseRequestSpec? database, MqttRequestSpec? mqtt)
    => new("test", null, null, new(method, path, null, null, null, mqtt, database), null, null, null, null, null);
static void Equal(string expected, string actual, string name)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
}
