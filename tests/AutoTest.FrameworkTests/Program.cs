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
using AutoTest.LoadTesting;
using AutoTest.Redis;

Environment.SetEnvironmentVariable("FRAMEWORK_TEST_URL", "http://localhost");
var environment = EnvironmentStore.Load(Path.GetTempFileName());
var variables = new Dictionary<string, string> { ["id"] = "42" };
Equal("items/42", Templates.Resolve("items/${id}", variables, environment), "template variable");
using JsonDocument integerDocument = JsonDocument.Parse("12");
using JsonDocument decimalDocument = JsonDocument.Parse("12.0");
Equal("true", JsonAssertionValues.Equal(decimalDocument.RootElement, integerDocument.RootElement, "12").ToString().ToLowerInvariant(), "numeric assertion ignores JSON number scale");

string commonEnvironmentPath = Path.GetTempFileName();
string projectEnvironmentPath = Path.GetTempFileName();
File.WriteAllText(commonEnvironmentPath, "SHARED_VALUE=common\nOVERRIDE_VALUE=common\n");
File.WriteAllText(projectEnvironmentPath, "PROJECT_VALUE=project\nOVERRIDE_VALUE=project\n");
var layeredEnvironment = EnvironmentStore.Load([commonEnvironmentPath, projectEnvironmentPath]);
Equal("common", layeredEnvironment.Get("SHARED_VALUE")!, "common environment value");
Equal("project", layeredEnvironment.Get("PROJECT_VALUE")!, "project environment value");
Equal("project", layeredEnvironment.Get("OVERRIDE_VALUE")!, "project environment overrides common value");
File.Delete(commonEnvironmentPath);
File.Delete(projectEnvironmentPath);

var registry = new StepExecutorRegistry(new ITestStepExecutor[]
{
    new HttpStepExecutor(
        new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
        environment),
    new PostgreSqlStepExecutor(),
    new MqttStepExecutor(environment),
    new HttpStubStepExecutor(environment),
    new RedisStepExecutor(),
});
Equal("http", registry.Resolve(Step("GET", "/health", null, null)).Name, "HTTP executor");
Equal("postgresql", registry.Resolve(Step(null, null, new("SELECT 1", null), null)).Name, "PostgreSQL executor");
Equal("mqtt", registry.Resolve(Step(null, null, null, new("connect", null, null, null, null, null, null, null, null, null))).Name, "MQTT executor");
var stubStep = new StepSpec("stub", null, null,
    new RequestSpec(null, null, null, null, null, null, null, new("start", null, null, null, null, null, null, null)),
    null, null, null, null, null);
Equal("http-stub", registry.Resolve(stubStep).Name, "HTTP stub executor");
var redisStep = new StepSpec("redis", null, null,
    new RequestSpec(null, null, null, null, null, null, null, null, null, null,
        new RedisRequestSpec("get", "key")), null, null, null, null, null);
Equal("redis", registry.Resolve(redisStep).Name, "Redis executor");
string redisValidationDirectory = Path.Combine(Path.GetTempPath(), $"autotest-redis-validation-{Guid.NewGuid():N}");
Directory.CreateDirectory(redisValidationDirectory);
IReadOnlyList<ValidationIssue> redisIssues = TestcaseValidator.Validate(redisValidationDirectory,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [new CaseSpec("redis.case", "Redis", ["redis"], false, null, [redisStep with { Expect = new ExpectSpec(null, null, null, null, null, null, null, new RedisExpectSpec(Exists: true)) }], null,
        new CoverageSpec(new SourceVerificationSpec(["source.cs"], DateTimeOffset.UtcNow.ToString("O"), null), new("covered", ["redis"], null), new("covered", ["redis"], null), new("notApplicable", null, "n/a"), new("notApplicable", null, "n/a"), new("notApplicable", null, "n/a"), new("notApplicable", null, "n/a"), new("notApplicable", null, "n/a"), new("covered", ["redis"], null)))],
    registry.All, validateTemporaryFiles: false);
Equal("0", redisIssues.Count.ToString(), "Redis testcase preflight");
Directory.Delete(redisValidationDirectory, true);
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
{"schemaVersion":1,"project":"framework-test","cases":[{"id":"sample.case","name":"Kịch bản mẫu","tags":["smoke"],"destructive":false,"steps":[{"name":"Đọc dữ liệu mẫu","request":{"method":"GET","path":"/health"},"expect":{"status":200}}]}]}
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

string coverageDirectory = Path.Combine(fixtureRoot, "coverage");
Directory.CreateDirectory(coverageDirectory);
File.WriteAllText(Path.Combine(coverageDirectory, "valid.json"), """
{
  "schemaVersion": 2,
  "project": "framework-test",
  "cases": [{
    "id": "coverage.valid",
    "name": "Kiểm tra manifest coverage hợp lệ",
    "tags": ["smoke"],
    "destructive": false,
    "steps": [{"name":"Đọc health","request":{"method":"GET","path":"/health"},"expect":{"status":200}}],
    "coverage": {
      "sourceVerification":{"files":["Controllers/HealthController.cs"],"verifiedAt":"2026-08-21T00:00:00Z","contractVersion":"test"},
      "contract":{"status":"covered","evidenceSteps":["Đọc health"]},
      "validation":{"status":"covered","evidenceSteps":["Đọc health"]},
      "authentication":{"status":"notApplicable","reason":"Endpoint public."},
      "scope":{"status":"notApplicable","reason":"Không có resource scope."},
      "integrity":{"status":"notApplicable","reason":"Request chỉ đọc."},
      "lifecycle":{"status":"notApplicable","reason":"Không phụ thuộc trạng thái."},
      "concurrency":{"status":"notApplicable","reason":"Không có tài nguyên tranh chấp."},
      "reporting":{"status":"covered","evidenceSteps":["Đọc health"]}
    }
  }]
}
""");
CaseSpec validCoverageCase = SpecLoader.Cases(coverageDirectory, "framework-test").Single();
IReadOnlyList<ValidationIssue> validCoverageIssues = TestcaseValidator.Validate(coverageDirectory,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [validCoverageCase], registry.All);
Equal("0", validCoverageIssues.Count.ToString(), "schema v2 valid coverage manifest");

CaseSpec missingCoverageCase = validCoverageCase with { Id = "coverage.missing", Coverage = null };
IReadOnlyList<ValidationIssue> missingCoverageIssues = TestcaseValidator.Validate(coverageDirectory,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [missingCoverageCase], registry.All, validateTemporaryFiles: false);
Equal("true", missingCoverageIssues.Any(issue => issue.Message.Contains("coverage manifest", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant(),
    "schema v2 rejects missing coverage manifest");

CoverageSpec invalidEvidence = validCoverageCase.Coverage! with
{
    Contract = new CoverageItemSpec("covered", ["Step không tồn tại"], null)
};
CaseSpec invalidEvidenceCase = validCoverageCase with { Id = "coverage.invalid-evidence", Coverage = invalidEvidence };
IReadOnlyList<ValidationIssue> invalidEvidenceIssues = TestcaseValidator.Validate(coverageDirectory,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [invalidEvidenceCase], registry.All, validateTemporaryFiles: false);
Equal("true", invalidEvidenceIssues.Any(issue => issue.Message.Contains("step không tồn tại", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant(),
    "schema v2 rejects unknown evidence step");
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

Equal("3000", LoadDiagnostics.Percentile(new double[] { 1000, 2000, 3000 }, .95).ToString("F0"), "load percentile nearest rank");
Equal("123.4", LoadDiagnostics.ParseServerTiming(new[] { "lock_wait;dur=123.4, db;dur=45.6" })["lock_wait"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture), "server timing parser");
Equal("lock-rejected", LoadDiagnostics.Category(423, null), "HTTP 423 category");
Equal("timeout", LoadDiagnostics.Category(0, nameof(TaskCanceledException)), "timeout category");
Equal("12", LoadDiagnostics.HashIdentifier("customer-1").Length.ToString(), "identifier hash length");
Equal("200", LoadDiagnostics.BarrierSkewMs(new[] { DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMilliseconds(200) }).ToString("F0"), "barrier skew");
Equal("2,323 ms", LoadDiagnostics.FormatMilliseconds(2323), "milliseconds formatting");
Equal("0", LoadDiagnostics.Percentile(Array.Empty<double>(), .95).ToString("F0"), "empty percentile");
Equal("45.6", LoadDiagnostics.ParseServerTiming(new[] { "db;desc=database;dur=45.6" })["db"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture), "server timing with description");

using (JsonDocument schemaDocument = JsonDocument.Parse("""{"type":"object","required":["id"],"properties":{"id":{"type":"integer"}}}"""))
using (JsonDocument validDocument = JsonDocument.Parse("""{"id":12}"""))
    AutoTest.Http.SimpleJsonSchema.Validate(validDocument.RootElement, schemaDocument.RootElement, "$");
Equal("true", File.Exists(Path.Combine(AppContext.BaseDirectory, "AutoTest.FrameworkTests.dll")).ToString().ToLowerInvariant(), "framework assembly exists");

string junitDirectory = Path.Combine(Path.GetTempPath(), $"autotest-junit-{Guid.NewGuid():N}");
string junitPath = AutoTest.Reporting.Html.JUnitReportWriter.Write(junitDirectory,
    new ProjectSpec("framework-test", "FRAMEWORK_TEST_URL", null, null, null),
    [new RunResult("sample", "Mẫu", true, TimeSpan.FromMilliseconds(10), null, [])], DateTimeOffset.UtcNow);
Equal("true", File.Exists(junitPath).ToString().ToLowerInvariant(), "JUnit report exists");
Directory.Delete(junitDirectory, true);

string generatorFixture = Path.Combine(Path.GetTempPath(), $"autotest-generator-{Guid.NewGuid():N}");
Directory.CreateDirectory(generatorFixture);
File.WriteAllText(Path.Combine(generatorFixture, "Fixture.cs"), """
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
[ApiController, Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost, ProducesResponseType(typeof(OrderResponse), 201), ProducesResponseType(400)]
    public Task<OrderResponse> Create([FromBody] CreateOrderRequest request) => throw new NotImplementedException();
    [HttpGet, ProducesResponseType(typeof(OrderResponse), 200)]
    public Task<OrderResponse> Search([FromQuery] SearchRequest request) { if (request.Page < 1) throw new ArgumentOutOfRangeException(); return OrderService.LoadAsync(request); }
}
[ApiController, Route("api/users")]
public sealed class UsersController : ControllerBase
{
    [AllowAnonymous, HttpPost("auth"), ProducesResponseType(typeof(AuthResponse), 200)]
    public Task<AuthResponse> Authenticate([FromBody] LoginRequest request) => throw new NotImplementedException();
    [Authorize, HttpPost, ProducesResponseType(typeof(OrderResponse), 200)]
    public Task<OrderResponse> CreateUser([FromForm] CreateUserRequest request) => throw new NotImplementedException();
}
public static class OrderService { public static Task<OrderResponse> LoadAsync(SearchRequest request) => throw new NotImplementedException(); }
public sealed record AddressRequest(string Street, string City);
public sealed class CreateOrderRequest { public required string Code { get; init; } public int Quantity { get; init; } public AddressRequest Address { get; init; } = default!; }
public class PagingRequest { public int Current { get; init; } public int PageSize { get; init; } }
public sealed class SearchRequest : PagingRequest { public string? Keyword { get; init; } }
public sealed class OrderResponse { public Guid Id { get; init; } [JsonPropertyName("orderCode")] public string Code { get; init; } = ""; public int Quantity { get; init; } }
public sealed record LoginRequest(string Username, string Password);
public sealed record AuthResponse(string AccessToken, string RefreshToken);
public sealed record CreateUserRequest(string Username, string Password);
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest> { public const int MaxQuantity = 20; public CreateOrderValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(10); RuleFor(x => x.Quantity).LessThanOrEqualTo(MaxQuantity); } }
""");
var generatorAnalysis = new AutoTest.Generator.SourceContractAnalyzer().Analyze(generatorFixture);
var generatorModules = AutoTest.Generator.SourceContractAnalyzer.DiscoverModules(generatorFixture);
Equal("true", generatorModules.Contains("Users", StringComparer.OrdinalIgnoreCase).ToString().ToLowerInvariant(), "generator discovers controller module");
var usersModuleAnalysis = new AutoTest.Generator.SourceContractAnalyzer().Analyze(generatorFixture, "Users");
Equal("2", usersModuleAnalysis.Endpoints.Count.ToString(), "generator module endpoint count");
Equal("4", generatorAnalysis.Endpoints.Count.ToString(), "generator endpoint count");
Equal("3", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Path == "/api/orders" && endpoint.Method == "POST").Validations.Count.ToString(), "generator validation count");
Equal("3", generatorAnalysis.Types["OrderResponse"].Properties.Count.ToString(), "generator response DTO properties");
Equal("20", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Path == "/api/orders" && endpoint.Method == "POST").Validations.Single(rule => rule.Rule == "LessThanOrEqualTo").Value!, "generator constant boundary");
Equal("1", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Method == "GET").ServiceCalls.Count.ToString(), "generator semantic service call");
Equal("1", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Method == "GET").Branches.Count.ToString(), "generator branch count");
Equal("login", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Path == "/api/users/auth").AuthenticationRole, "generator login endpoint role");
Equal("user-create", generatorAnalysis.Endpoints.Single(endpoint => endpoint.Path == "/api/users" && endpoint.Method == "POST").AuthenticationRole, "generator create-user endpoint role");
Equal("/api/users/auth", generatorAnalysis.Authentication.SuggestedConfiguration!.LoginPath, "generator suggested login path");
Equal("AUTH_USERNAME", generatorAnalysis.Authentication.SuggestedConfiguration.RequiredEnvironmentVariables.First(), "generator auth environment variable");
var generatedSuite = AutoTest.Generator.DraftSuiteBuilder.Build("ops-service", generatorAnalysis, null);
var localGeneratorOptions = AutoTest.Generator.GeneratorExecutionOptions.From("local", null, null, null);
Equal("0", localGeneratorOptions.TimeoutSeconds.ToString(), "hybrid generator has unlimited AI timeout by default");
var localGeneratorResult = await AutoTest.Generator.HybridDraftGenerator.BuildAsync("ops-service", generatorFixture, generatorAnalysis, null, localGeneratorOptions);
Equal("local-roslyn", localGeneratorResult.Provider, "hybrid generator local provider");
Equal("false", localGeneratorResult.AiUsed.ToString().ToLowerInvariant(), "hybrid generator local AI flag");
Equal(generatedSuite.Cases.Count.ToString(), localGeneratorResult.Suite.Cases.Count.ToString(), "hybrid generator local suite");
string codexResolutionDirectory = Path.Combine(Path.GetTempPath(), $"toolqc-codex-resolution-{Guid.NewGuid():N}");
Directory.CreateDirectory(codexResolutionDirectory);
string fakeCodex = Path.Combine(codexResolutionDirectory, OperatingSystem.IsWindows() ? "codex.cmd" : "codex");
File.WriteAllText(fakeCodex, string.Empty);
var resolvedCodex = AutoTest.Generator.HybridDraftGenerator.ResolveCodexCommand(codexResolutionDirectory);
Equal("true", resolvedCodex.DisplayName.EndsWith(Path.GetFileName(fakeCodex), StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant(), "hybrid generator resolves Codex shim");
Equal("claude-cli", AutoTest.Generator.Ai.AiProviderFactory.Create("claude-cli").Name, "hybrid generator Claude provider");
Directory.Delete(codexResolutionDirectory, true);
string generatedJson = System.Text.Json.JsonSerializer.Serialize(generatedSuite, AutoTest.Generator.GeneratorJson.Options);
Equal("true", generatedJson.Contains("\"address\"", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator nested DTO body");
Equal("false", generatedJson.Contains("\"code\": {}", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator scalar string body");
Equal("true", generatedJson.Contains("AUTO${shortUniqueA}", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator short unique code");
Equal("true", (generatedJson.Contains("?current=1\\u0026pageSize=10", StringComparison.Ordinal) || generatedJson.Contains("?current=1&pageSize=10", StringComparison.Ordinal)).ToString().ToLowerInvariant(), "generator inherited query DTO");
Equal("true", generatedJson.Contains("\"quantity\": 21", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant(), "generator resolved invalid boundary");
Equal("true", generatedJson.Contains("generated.post-api-orders.validation.code.maximumlength", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator boundary case");
Equal("true", generatedJson.Contains("$.data.orderCode", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator response assertion");
Equal("true", generatedJson.Contains("${env:AUTH_USERNAME}", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator login uses environment username");
Equal("true", generatedJson.Contains("${env:AUTH_PASSWORD}", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator login uses environment password");
Equal("true", generatedJson.Contains("\"authToken\": \"$.data.accessToken\"", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator login saves token for chained requests");
Equal("true", generatedJson.Contains("\"form\"", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator FromForm request uses multipart form");
Equal("false", generatedJson.Contains("\"avatarPicture\"", StringComparison.Ordinal).ToString().ToLowerInvariant(), "generator omits unsupported file placeholder");
string cobertura = Path.Combine(generatorFixture, "coverage.cobertura.xml");
File.WriteAllText(cobertura, """<coverage lines-covered="8" lines-valid="10" branches-covered="1" branches-valid="2"><packages><package><classes><class filename="OrdersController.cs"><methods><method name="Search"><lines><line number="10" hits="0" branch="true" condition-coverage="50% (1/2)" /></lines></method></methods></class></classes></package></packages></coverage>""");
var coverageAnalysis = AutoTest.Generator.CoberturaCoverageReader.Read(cobertura);
Equal("0.8", coverageAnalysis.LineRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "generator line coverage");
Equal("2", coverageAnalysis.Gaps.Count.ToString(), "generator coverage gaps");
Directory.Delete(generatorFixture, true);

Console.WriteLine("Framework tests passed: 56");
return 0;

static StepSpec Step(string? method, string? path, DatabaseRequestSpec? database, MqttRequestSpec? mqtt)
    => new("test", null, null, new(method, path, null, null, null, mqtt, database), null, null, null, null, null);
static void Equal(string expected, string actual, string name)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"{name}: expected '{expected}', actual '{actual}'");
}
