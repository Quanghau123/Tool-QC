using System.Diagnostics;
using System.Text.Json;
using AutoTest.Abstractions;

namespace AutoTest.HttpStub;

public sealed class HttpStubStepExecutor : ITestStepExecutor
{
    private readonly HttpStubServer server;
    public HttpStubStepExecutor(IEnvironmentStore environment) => server = new HttpStubServer(environment);
    public string Name => "http-stub";
    public bool CanExecute(StepSpec step) => step.Request?.HttpStub is not null;

    public async Task<StepRunResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        HttpStubRequestSpec spec = context.Step.Request!.HttpStub!;
        string action = spec.Action.Trim().ToLowerInvariant();
        try
        {
            object actual = action switch
            {
                "start" => await server.StartAsync(cancellationToken),
                "configure" => server.Configure(spec, context),
                "reset" => server.Reset(),
                "inspect" => await server.InspectAsync(spec, context, cancellationToken),
                "stop" => await server.StopAsync(cancellationToken),
                _ => throw new InvalidDataException($"HttpStub action không được hỗ trợ: {spec.Action}")
            };
            string output = JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true });
            return new(context.Step.Name, context.Cleanup, true, "HTTP-STUB", action, null,
                Expected(context.Step.Expect?.HttpStub, action), null, output, watch.Elapsed, null);
        }
        catch (Exception exception)
        {
            return new(context.Step.Name, context.Cleanup, false, "HTTP-STUB", action, null,
                Expected(context.Step.Expect?.HttpStub, action), null, null, watch.Elapsed,
                context.Redact?.Invoke(exception.Message) ?? exception.Message);
        }
    }

    private static string Expected(HttpStubExpectSpec? expect, string action)
        => expect is null ? $"HttpStub thực hiện action {action}" : JsonSerializer.Serialize(expect);

    public async ValueTask DisposeAsync() => await server.DisposeAsync();
}
