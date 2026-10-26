using System.Diagnostics;

namespace AutoTest.Web;

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

public static class ProcessExecutor
{
    public static async Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new(process.ExitCode, (await outputTask).Trim(), (await errorTask).Trim());
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
