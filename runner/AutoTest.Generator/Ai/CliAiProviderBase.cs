using System.Diagnostics;
using System.Text;

namespace AutoTest.Generator.Ai;

public abstract class CliAiProviderBase : IAiGeneratorProvider
{
    public abstract string Name { get; }
    protected abstract string CommandName { get; }
    protected abstract IReadOnlyList<string> VersionArguments { get; }
    protected abstract IReadOnlyList<string> AuthenticationArguments { get; }
    protected abstract IReadOnlyList<string> ExecutionArguments { get; }
    protected abstract bool IsAuthenticated(int exitCode, string output, string error);

    public async Task<AiProviderStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AiCliCommand command = AiCliProcess.Resolve(CommandName);
            AiCliResult version = await AiCliProcess.RunAsync(command, VersionArguments, null, null, 10, cancellationToken);
            if (version.ExitCode != 0) return new(Name, false, false, command.DisplayName, string.Empty, AiCliProcess.Short(version.Error));
            AiCliResult auth = await AiCliProcess.RunAsync(command, AuthenticationArguments, null, null, 10, cancellationToken);
            bool authenticated = IsAuthenticated(auth.ExitCode, auth.Output, auth.Error);
            return new(Name, true, authenticated, command.DisplayName, version.Output.Trim(), authenticated ? $"{Name} is ready." : $"{Name} is installed but not logged in.");
        }
        catch (Exception exception)
        {
            return new(Name, false, false, string.Empty, string.Empty, AiCliProcess.Short(exception.Message));
        }
    }

    public async Task<string> ExecuteAsync(string sourceDirectory, string prompt, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        AiCliCommand command = AiCliProcess.Resolve(CommandName);
        AiCliResult result = await AiCliProcess.RunAsync(command, ExecutionArguments, sourceDirectory, prompt, timeoutSeconds, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? $"{Name} exited with code {result.ExitCode}." : result.Error.Trim());
        if (string.IsNullOrWhiteSpace(result.Output)) throw new InvalidDataException($"{Name} did not return output.");
        return result.Output.Trim();
    }
}

public sealed record AiCliCommand(string FileName, IReadOnlyList<string> PrefixArguments, string DisplayName);
public sealed record AiCliResult(int ExitCode, string Output, string Error);

public static class AiCliProcess
{
    public static AiCliCommand Resolve(string commandName, string? searchPath = null)
    {
        string pathValue = searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] names = OperatingSystem.IsWindows() ? [$"{commandName}.exe", $"{commandName}.cmd", $"{commandName}.bat", commandName] : [commandName];
        foreach (string name in names)
            foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string candidate;
                try { candidate = Path.Combine(directory.Trim('"'), name); } catch { continue; }
                if (!File.Exists(candidate)) continue;
                string extension = Path.GetExtension(candidate);
                return OperatingSystem.IsWindows() && extension is ".cmd" or ".bat"
                    ? new(Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe", ["/d", "/s", "/c", candidate], candidate)
                    : new(candidate, [], candidate);
            }
        throw new FileNotFoundException($"{commandName} CLI was not found in PATH.");
    }

    public static async Task<AiCliResult> RunAsync(AiCliCommand command, IReadOnlyList<string> arguments, string? workingDirectory, string? input, int timeoutSeconds, CancellationToken cancellationToken)
    {
        ProcessStartInfo info = new(command.FileName)
        {
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (input is not null) info.StandardInputEncoding = Encoding.UTF8;
        foreach (string argument in command.PrefixArguments.Concat(arguments)) info.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = info };
        process.Start();
        if (input is not null) { await process.StandardInput.WriteAsync(input); process.StandardInput.Close(); }
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource? timeout = timeoutSeconds > 0 ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        timeout?.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout?.Token ?? cancellationToken); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw timeoutSeconds > 0 ? new TimeoutException($"{command.DisplayName} exceeded {timeoutSeconds} seconds.") : new OperationCanceledException(cancellationToken);
        }
        return new(process.ExitCode, (await output).Trim(), (await error).Trim());
    }

    public static string Short(string value) => value.Length <= 240 ? value : value[..240];
}
