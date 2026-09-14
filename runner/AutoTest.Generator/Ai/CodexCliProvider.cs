namespace AutoTest.Generator.Ai;

public sealed class CodexCliProvider : CliAiProviderBase
{
    public override string Name => "codex-cli";
    protected override string CommandName => "codex";
    protected override IReadOnlyList<string> VersionArguments => ["--version"];
    protected override IReadOnlyList<string> AuthenticationArguments => ["login", "status"];
    protected override IReadOnlyList<string> ExecutionArguments => ["exec", "-", "--ephemeral", "--color", "never", "--sandbox", "read-only", "--skip-git-repo-check"];
    protected override bool IsAuthenticated(int exitCode, string output, string error) => exitCode == 0;
}
