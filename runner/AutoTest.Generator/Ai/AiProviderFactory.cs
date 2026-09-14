namespace AutoTest.Generator.Ai;

public static class AiProviderFactory
{
    public static IAiGeneratorProvider Create(string provider)
        => provider.Trim().ToLowerInvariant() switch
        {
            "codex-cli" => new CodexCliProvider(),
            "claude-cli" => new ClaudeCliProvider(),
            _ => throw new InvalidOperationException($"Unsupported AI provider: {provider}. Supported providers: codex-cli, claude-cli."),
        };
}
