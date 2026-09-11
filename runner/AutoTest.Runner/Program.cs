using AutoTest.Core;
using AutoTest.Abstractions;
using AutoTest.Reporting.Html;
using AutoTest.Http;
using AutoTest.Mqtt;
using AutoTest.PostgreSql;
using AutoTest.Elasticsearch;
using AutoTest.HttpStub;
using AutoTest.TestValidation;
using AutoTest.Redis;
// CLI runner: nạp project, preflight, chạy testcase song song và xuất báo cáo.
var startedAt=DateTimeOffset.Now;
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
string Arg(string key,string fallback){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
var rootEnvironment=EnvironmentStore.Load(Path.Combine(root,".env"));
var projectName=Arg("--project",rootEnvironment.Get("ACTIVE_PROJECT")??"ops-service");
var projectDir=Path.Combine(root,"projects",projectName);
var testcaseDirectory=Path.GetFullPath(Arg("--testcase-directory",Path.Combine(projectDir,"testcases")));
var allowedTestcaseRoot=Path.GetFullPath(Path.Combine(root,".artifacts","generated-testcases"));
if(!testcaseDirectory.StartsWith(Path.GetFullPath(projectDir)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&!testcaseDirectory.StartsWith(allowedTestcaseRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){Console.Error.WriteLine("Thư mục testcase nằm ngoài phạm vi an toàn.");return 2;}
var env=EnvironmentStore.Load([Path.Combine(root,".env"),Path.Combine(projectDir,".env")]);
var tags=Arg("--tags",env.Get("TEST_TAGS")??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
var caseIds=Arg("--case-ids","").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
var project=SpecLoader.Project(Path.Combine(projectDir,"project.json"));
var cases=SpecLoader.Cases(testcaseDirectory,project.Name)
    .Where(c=>caseIds.Length==0||caseIds.Contains(c.Id,StringComparer.OrdinalIgnoreCase))
    .Where(c=>tags.Length==0||c.Tags?.Intersect(tags,StringComparer.OrdinalIgnoreCase).Any()==true)
    .ToList();
if(cases.Count==0){Console.Error.WriteLine("Không tìm thấy kịch bản kiểm thử phù hợp.");return 2;}
if(caseIds.Length>0)
{
    var missing=caseIds.Except(cases.Select(c=>c.Id),StringComparer.OrdinalIgnoreCase).ToArray();
    if(missing.Length>0){Console.Error.WriteLine($"Không tìm thấy testcase ID: {string.Join(", ",missing)}");return 2;}
}
await using(var validationRegistry=new StepExecutorRegistry(new ITestStepExecutor[]{new HttpStepExecutor(project,env),new PostgreSqlStepExecutor(),new ElasticsearchStepExecutor(),new MqttStepExecutor(env),new HttpStubStepExecutor(env),new RedisStepExecutor()}))
{
    var issues=TestcaseValidator.Validate(testcaseDirectory,project,cases,validationRegistry.All,validateTemporaryFiles:tags.Length==0&&caseIds.Length==0);
    if(issues.Count>0){Console.Error.WriteLine($"Preflight phát hiện {issues.Count} lỗi testcase:");foreach(var issue in issues)Console.Error.WriteLine($"- {issue.Location}: {issue.Message}");return 3;}
}
if(args.Contains("--preflight-only",StringComparer.OrdinalIgnoreCase)){Console.WriteLine($"Preflight thành công cho {cases.Count} testcase.");return 0;}
var maxParallelCases=Math.Max(1,env.Int("MAX_PARALLEL_CASES",1));
var resultSlots=new RunResult[cases.Count];
await Parallel.ForEachAsync(Enumerable.Range(0,cases.Count),new ParallelOptions{MaxDegreeOfParallelism=maxParallelCases},async(index,cancellationToken)=>
{
    using var engine=new RunnerEngine(project,env);
    var result=await engine.RunAsync(cases[index],cancellationToken);
    resultSlots[index]=result;
    Console.WriteLine($"[{(result.Passed?"THÀNH CÔNG":"THẤT BẠI")}] {result.Id} - {result.Name} ({result.Duration.TotalMilliseconds:F0}ms)");
    if(!result.Passed)Console.Error.WriteLine($"  {result.Error}");
});
var results=resultSlots.ToList();
var failed=results.Count(result=>!result.Passed);
var configuredReportDirectory=env.Get("TEST_RESULTS_DIR")??Path.Combine(".artifacts","test-runs");
var reportDirectory=Path.IsPathRooted(configuredReportDirectory)
    ? configuredReportDirectory
    : Path.GetFullPath(Path.Combine(root,configuredReportDirectory));
var sourceGroups=cases.Select(test=>test.SourceGroup).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
var reportGroup=sourceGroups.Length==1?sourceGroups[0]:"_combined";
reportDirectory=Path.Combine(reportDirectory,project.Name,reportGroup);
IReportWriter reportWriter=new HtmlReportModule();
var report=reportWriter.Write(reportDirectory,project,env.Get("TEST_ENV")??"unspecified",results,startedAt,tags);
if(env.Bool("JUNIT_REPORT_ENABLED",true)){var junit=JUnitReportWriter.Write(reportDirectory,project,results,startedAt);Console.WriteLine($"Báo cáo JUnit: {junit}");}
Console.WriteLine($"Tổng số: {cases.Count}, Thành công: {cases.Count-failed}, Thất bại: {failed}");
Console.WriteLine($"Báo cáo HTML: {report}");return failed==0?0:1;
