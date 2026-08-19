using AutoTest.Core;
using AutoTest.Abstractions;
using AutoTest.Reporting.Html;
using AutoTest.Http;
using AutoTest.Mqtt;
using AutoTest.PostgreSql;
using AutoTest.TestValidation;
var startedAt=DateTimeOffset.Now;
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
var env=EnvironmentStore.Load(Path.Combine(root,".env"));
string Arg(string key,string fallback){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
var projectName=Arg("--project",env.Get("ACTIVE_PROJECT")??"ops-service");
var tags=Arg("--tags",env.Get("TEST_TAGS")??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
var projectDir=Path.Combine(root,"projects",projectName);
var project=SpecLoader.Project(Path.Combine(projectDir,"project.json"));
var cases=SpecLoader.Cases(Path.Combine(projectDir,"testcases"),project.Name).Where(c=>tags.Length==0||c.Tags?.Intersect(tags,StringComparer.OrdinalIgnoreCase).Any()==true).ToList();
if(cases.Count==0){Console.Error.WriteLine("Không tìm thấy kịch bản kiểm thử phù hợp.");return 2;}
await using(var validationRegistry=new StepExecutorRegistry(new ITestStepExecutor[]{new HttpStepExecutor(project,env),new PostgreSqlStepExecutor(),new MqttStepExecutor(env)}))
{
    var issues=TestcaseValidator.Validate(Path.Combine(projectDir,"testcases"),project,cases,validationRegistry.All,validateTemporaryFiles:tags.Length==0);
    if(issues.Count>0){Console.Error.WriteLine($"Preflight phát hiện {issues.Count} lỗi testcase:");foreach(var issue in issues)Console.Error.WriteLine($"- {issue.Location}: {issue.Message}");return 3;}
}
using var engine=new RunnerEngine(project,env);var failed=0;var results=new List<RunResult>();
foreach(var test in cases){var result=await engine.RunAsync(test);results.Add(result);Console.WriteLine($"[{(result.Passed?"THÀNH CÔNG":"THẤT BẠI")}] {result.Id} - {result.Name} ({result.Duration.TotalMilliseconds:F0}ms)");if(!result.Passed){failed++;Console.Error.WriteLine($"  {result.Error}");}}
var configuredReportDirectory=env.Get("TEST_RESULTS_DIR")??"test-results";
var reportDirectory=Path.IsPathRooted(configuredReportDirectory)
    ? configuredReportDirectory
    : Path.GetFullPath(Path.Combine(root,configuredReportDirectory));
var sourceGroups=cases.Select(test=>test.SourceGroup).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
var reportGroup=sourceGroups.Length==1?sourceGroups[0]:"_combined";
reportDirectory=Path.Combine(reportDirectory,project.Name,reportGroup);
IReportWriter reportWriter=new HtmlReportModule();
var report=reportWriter.Write(reportDirectory,project,env.Get("TEST_ENV")??"unspecified",results,startedAt,tags);
Console.WriteLine($"Tổng số: {cases.Count}, Thành công: {cases.Count-failed}, Thất bại: {failed}");
Console.WriteLine($"Báo cáo HTML: {report}");return failed==0?0:1;
