using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTest.Integration.Abstractions;
using AutoTest.Integration.Artifacts;
using AutoTest.Integration.Http;

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, WriteIndented = true };
string root = FindRoot();
if (args.Length == 0 || Has("--help") || Has("-h")) { Help(); return; }

if (Has("--config"))
{
    string path = Resolve(Argument("--config") ?? throw new InvalidDataException("Thiếu --config."));
    LegacyConfiguration legacy = JsonSerializer.Deserialize<LegacyConfiguration>(await File.ReadAllTextAsync(path), options) ?? throw new InvalidDataException("Không đọc được cấu hình cũ.");
    var profile = new IntegrationProfile("legacy-http-stub", "http", Argument("--url") ?? "http://127.0.0.1:2669", legacy.Routes, true);
    await RunAsync("legacy", profile.Name, path, profile);
    return;
}

string command = args[0].ToLowerInvariant();
if (command == "list")
{
    foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "projects"), "integration.json", SearchOption.AllDirectories))
        Console.WriteLine(Path.GetRelativePath(Path.Combine(root, "projects"), file).Replace("\\integration.json", ""));
    return;
}
if (command == "inspect")
{
    Need(3); string directory = IntegrationDirectory(args[1], args[2]);
    string? current = CurrentSession(directory);
    string? session = Argument("--session") ?? current ?? Directory.EnumerateDirectories(directory).Select(Path.GetFileName).Where(x => x?[0] is >= '0' and <= '9').OrderDescending().FirstOrDefault();
    if (session is null) throw new InvalidOperationException("Chưa có integration artifact.");
    Console.WriteLine(Path.Combine(directory, session, "index.html"));
    return;
}

Need(3);
string project = args[1], integration = args[2];
IntegrationProfile selected = await ProfileAsync(project, integration);
if (command is "status" or "stop")
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    HostHealth? health;
    try { health = await client.GetFromJsonAsync<HostHealth>($"{selected.Url}/__autotest/health", options); }
    catch (Exception exception) { Console.Error.WriteLine($"Integration '{project}/{integration}' không chạy: {exception.Message}"); Environment.ExitCode = 2; return; }
    if (health is null || health.Project != project || health.Integration != integration)
    { Console.Error.WriteLine("Cổng đang thuộc Integration Host khác; từ chối thao tác."); Environment.ExitCode = 3; return; }
    if (command == "status") { Console.WriteLine(JsonSerializer.Serialize(health, options)); return; }
    string expectedToken = CurrentToken(IntegrationDirectory(project, integration)) ?? throw new InvalidOperationException("Thiếu ownership token; từ chối stop.");
    if (expectedToken != health.InstanceToken) throw new InvalidOperationException("Ownership token không khớp; từ chối stop.");
    using var request = new HttpRequestMessage(HttpMethod.Post, $"{selected.Url}/__autotest/shutdown");
    request.Headers.Add("X-AutoTest-Instance", expectedToken);
    using HttpResponseMessage response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
    Console.WriteLine($"Đã yêu cầu dừng Integration Host '{project}/{integration}'."); return;
}
if (command != "start") throw new InvalidDataException($"Lệnh không hỗ trợ: {command}");
if (Has("--background") && !Has("--child"))
{
    string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Không xác định được executable.");
    string prefix = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? $"\"{typeof(Program).Assembly.Location}\" " : "";
    Process process = Process.Start(new ProcessStartInfo(executable, $"{prefix}start \"{project}\" \"{integration}\" --child") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true }) ?? throw new InvalidOperationException("Không khởi động được host nền.");
    Console.WriteLine($"Integration Host đang khởi động nền. PID={process.Id}"); return;
}
await RunAsync(project, integration, ProfilePath(project, integration), selected);

async Task RunAsync(string projectName, string integrationName, string configPath, IntegrationProfile profile)
{
    if (!profile.Transport.Equals("http", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException($"Transport chưa hỗ trợ: {profile.Transport}");
    string sessionId = DateTimeOffset.Now.ToString("yyyy-MM-dd_HHmmss_fff");
    string directory = IntegrationDirectory(projectName, integrationName);
    string artifactRoot = Path.Combine(directory, sessionId);
    Directory.CreateDirectory(directory);
    string token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
    var session = new IntegrationSession(sessionId, projectName, integrationName, profile.Transport, profile.Url, configPath, Environment.ProcessId, DateTimeOffset.Now, "running", token);
    await File.WriteAllTextAsync(Path.Combine(directory, "current.json"), JsonSerializer.Serialize(new { sessionId, instanceToken = token, processId = Environment.ProcessId, artifactRoot }, options));
    var writer = new IntegrationArtifactWriter(artifactRoot);
    Console.WriteLine($"Integration Host '{projectName}/{integrationName}' chạy tại {profile.Url}");
    Console.WriteLine($"Artifact: {artifactRoot}");
    try { await new HttpIntegrationHost(profile, session, writer).RunAsync(); }
    finally { TryDelete(Path.Combine(directory, "current.json")); }
}

async Task<IntegrationProfile> ProfileAsync(string p, string i) => JsonSerializer.Deserialize<IntegrationProfile>(await File.ReadAllTextAsync(ProfilePath(p, i)), options) ?? throw new InvalidDataException("Không đọc được integration profile.");
string ProfilePath(string p, string i) => Path.Combine(root, "projects", p, "integrations", i, "integration.json");
string IntegrationDirectory(string p, string i) => Path.Combine(root, "integration-results", Safe(p), Safe(i));
string? CurrentToken(string directory) { string path = Path.Combine(directory, "current.json"); if (!File.Exists(path)) return null; using JsonDocument d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.GetProperty("instanceToken").GetString(); }
string? CurrentSession(string directory) { string path = Path.Combine(directory, "current.json"); if (!File.Exists(path)) return null; using JsonDocument d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.GetProperty("sessionId").GetString(); }
string FindRoot() { string? d = Directory.GetCurrentDirectory(); while (d is not null) { if (File.Exists(Path.Combine(d, "ApiAutoTest.sln"))) return d; d = Directory.GetParent(d)?.FullName; } throw new DirectoryNotFoundException("Không tìm thấy Tool-QC root."); }
string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(root, path));
string? Argument(string key) { int index = Array.FindIndex(args, x => x.Equals(key, StringComparison.OrdinalIgnoreCase)); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
bool Has(string value) => args.Contains(value, StringComparer.OrdinalIgnoreCase);
void Need(int count) { if (args.Length < count) throw new InvalidDataException("Thiếu project hoặc integration."); }
void TryDelete(string path) { try { File.Delete(path); } catch { } }
string Safe(string value) => string.Concat(value.Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x));
void Help() => Console.WriteLine("Tool-QC Integration Host\n  list\n  start <project> <name> [--background]\n  status <project> <name>\n  inspect <project> <name> [--session id]\n  stop <project> <name>");

sealed record LegacyConfiguration(List<HttpIntegrationRule> Routes);
sealed record HostHealth(string Status, string Transport, string Project, string Integration, int Routes, string SessionId, string ResultRoot, string InstanceToken);
