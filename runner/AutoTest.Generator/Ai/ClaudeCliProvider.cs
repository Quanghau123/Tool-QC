namespace AutoTest.Generator.Ai;

public sealed class ClaudeCliProvider : CliAiProviderBase
{
    public override string Name => "claude-cli";
    protected override string CommandName => "claude";
    protected override IReadOnlyList<string> VersionArguments => ["--version"];
    protected override IReadOnlyList<string> AuthenticationArguments => ["auth", "status"];
    protected override IReadOnlyList<string> ExecutionArguments => ["--print", "--output-format", "text", "--permission-mode", "manual", "--restricted"];
    protected override bool IsAuthenticated(int exitCode, string output, string error)
        => exitCode == 0 && output.Contains("\"loggedIn\": true", StringComparison.OrdinalIgnoreCase);
}
