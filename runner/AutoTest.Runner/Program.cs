using AutoTest.Core;
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
using var engine=new RunnerEngine(project,env);var failed=0;var results=new List<RunResult>();
foreach(var test in cases){var result=await engine.RunAsync(test);results.Add(result);Console.WriteLine($"[{(result.Passed?"THÀNH CÔNG":"THẤT BẠI")}] {result.Id} - {result.Name} ({result.Duration.TotalMilliseconds:F0}ms)");if(!result.Passed){failed++;Console.Error.WriteLine($"  {result.Error}");}}
var configuredReportDirectory=env.Get("TEST_RESULTS_DIR")??"test-results";
var reportDirectory=Path.IsPathRooted(configuredReportDirectory)
    ? configuredReportDirectory
    : Path.GetFullPath(Path.Combine(root,configuredReportDirectory));
var report=HtmlReportWriter.Write(reportDirectory,project,env.Get("TEST_ENV")??"unspecified",results,startedAt,tags);
Console.WriteLine($"Tổng số: {cases.Count}, Thành công: {cases.Count-failed}, Thất bại: {failed}");
Console.WriteLine($"Báo cáo HTML: {report}");return failed==0?0:1;
