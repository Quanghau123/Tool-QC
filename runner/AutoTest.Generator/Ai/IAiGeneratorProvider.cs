namespace AutoTest.Generator.Ai;

public interface IAiGeneratorProvider
{
    string Name { get; }
    Task<AiProviderStatus> ProbeAsync(CancellationToken cancellationToken = default);
    Task<string> ExecuteAsync(string sourceDirectory, string prompt, int timeoutSeconds, CancellationToken cancellationToken = default);
}
