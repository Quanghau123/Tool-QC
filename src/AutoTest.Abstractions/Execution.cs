namespace AutoTest.Abstractions;

public interface ITestStepExecutor : IAsyncDisposable
{
    string Name { get; }
    bool CanExecute(StepSpec step);
    Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken);
}

// Contract truyền dữ liệu giữa core và executor; thêm transport mới cần kiểm tra nhóm này.
public sealed record StepExecutionContext(
    StepSpec Step,
    ProjectSpec Project,
    IEnvironmentStore Environment,
    Dictionary<string, string> Variables,
    bool Assertions,
    bool Cleanup,
    Action<StepRunResult>? ReportIntermediate = null,
    Func<CancellationToken, Task<string?>>? ResolveAuthenticationToken = null,
    Func<string, string>? Redact = null);

public interface IEnvironmentStore
{
    string? Get(string key);
    string Require(string key);
    bool Bool(string key, bool fallback = false);
    int Int(string key, int fallback = 0);
}

public interface IReportWriter
{
    string Write(string directory, ProjectSpec project, string environment,
        IReadOnlyList<RunResult> results, DateTimeOffset startedAt,
        IReadOnlyCollection<string>? selectedTags = null);
}

public sealed class StepExecutorRegistry : IAsyncDisposable
{
    private readonly IReadOnlyList<ITestStepExecutor> executors;
    public StepExecutorRegistry(IEnumerable<ITestStepExecutor> executors) => this.executors = executors.ToArray();
    // Bảo đảm một step chỉ khớp đúng một executor, tránh chạy nhầm transport.
    public ITestStepExecutor Resolve(StepSpec step)
    {
        ITestStepExecutor[] matches = executors.Where(executor => executor.CanExecute(step)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException($"Không có executor phù hợp cho bước: {step.Name}"),
            _ => throw new InvalidDataException($"Có nhiều executor cùng nhận bước: {step.Name}"),
        };
    }
    public IReadOnlyList<ITestStepExecutor> All => executors;

    public async ValueTask DisposeAsync()
    {
        foreach (ITestStepExecutor executor in executors)
            await executor.DisposeAsync();
    }
}
