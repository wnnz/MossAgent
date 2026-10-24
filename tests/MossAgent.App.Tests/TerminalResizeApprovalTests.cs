using Avalonia.Headless;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalResizeApprovalTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static TerminalStateProbe EmitSource(TerminalSessionSnapshot state) { var s = new TerminalStateProbe(); s.Emit(state); return s; }
    private static ToolExecutionContext Context() =>
        new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.AskEveryTime);
    private static TerminalSessionSnapshot State(ToolExecutionContext context) =>
        new(context.TaskId, Guid.NewGuid(), 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default);
    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    [Fact]
    public Task AskEveryTimeRequestsNeverReachExecutorUntilExplicitApply() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var requests = new List<ToolRequest>();
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        {
            Execute = (request, captured, _) => { Assert.Same(context, captured); requests.Add(request); return Task.FromResult(ToolResult.Success("ok")); }
        }, EmitSource(state));
        resize.Attach(context, state, 1, default);
        Assert.True(resize.RequiresExplicitResize);

        resize.ResizeCommand.Execute(new(40, 10));
        resize.ResizeCommand.Execute(new(41, 11));
        resize.ResizeCommand.Execute(new(42, 12));
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.Empty(requests);
        Assert.True(resize.ApplyCommand.CanExecute(null));

        resize.ApplyCommand.Execute(null);
        await WaitAsync(() => requests.Count == 1);
        Assert.Equal(42, requests[0].Arguments.GetProperty("columns").GetInt32());
        Assert.Equal(12, requests[0].Arguments.GetProperty("rows").GetInt32());
        Assert.Equal(state.SessionId.ToString("D"), requests[0].Arguments.GetProperty("sessionId").GetString());
        Assert.Single(requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task GeometryChangeDuringApprovalDoesNotAutoTriggerSecondRequest() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => { requests.Add(request); return requests.Count == 1 ? pending.Task : Task.FromResult(ToolResult.Success("ok")); }
        }, EmitSource(state));
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        resize.ApplyCommand.Execute(null);
        await WaitAsync(() => requests.Count == 1);

        resize.ResizeCommand.Execute(new(50, 15));
        pending.SetResult(ToolResult.Success("ok"));
        await WaitAsync(() => !resize.IsBusy);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Single(requests);
        Assert.False(resize.HasNotice);

        Assert.True(resize.ApplyCommand.CanExecute(null));
        resize.ApplyCommand.Execute(null);
        await WaitAsync(() => requests.Count == 2);
        Assert.Equal(50, requests[1].Arguments.GetProperty("columns").GetInt32());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DeniedApplyStaysBlockedUntilExplicitRetry() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var calls = 0;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => Task.FromResult(++calls == 1 ? ToolResult.Failure("x", "approval_denied") : ToolResult.Success("ok")) },
            EmitSource(state));
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        resize.ApplyCommand.Execute(null);
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(1, calls);
        Assert.Contains("许可", resize.Notice);

        resize.ResizeCommand.Execute(new(45, 12));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        resize.RetryCommand.Execute(null);
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(2, calls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SessionReplacementDuringApprovalDoesNotReplayAgainstNewSession() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = EmitSource(state);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (request, _, _) => { requests.Add(request); return pending.Task; } }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        resize.ApplyCommand.Execute(null);
        await WaitAsync(() => requests.Count == 1);
        var replacement = state with { SessionId = Guid.NewGuid(), Revision = 2 };
        source.Emit(replacement);
        resize.Observe(replacement);
        pending.SetResult(ToolResult.Success("ok"));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Single(requests);
        Assert.Equal(state.SessionId.ToString("D"), requests[0].Arguments.GetProperty("sessionId").GetString());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SessionReplacedBeforeDebounceNeverReachesExecutor() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = EmitSource(state);
        var requests = new List<ToolRequest>();
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (request, _, _) => { requests.Add(request); return Task.FromResult(ToolResult.Success("ok")); } }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        resize.ApplyCommand.Execute(null);
        var replacement = state with { SessionId = Guid.NewGuid(), Revision = 2 };
        source.Emit(replacement);
        resize.Observe(replacement);
        await WaitAsync(() => !resize.IsBusy);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReadOnlyAndCancelledScopeNeverExecuteApply() => _session.DispatchAsync(async () =>
    {
        var readOnly = Context() with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        var calls = 0;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, new TerminalStateProbe());
        resize.Attach(readOnly, State(readOnly), 1, default);
        Assert.False(resize.ApplyCommand.CanExecute(null));
        Assert.False(resize.RequiresExplicitResize);
        resize.ApplyCommand.Execute(null);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(0, calls);

        var context = Context();
        var state = State(context);
        using var scope = new CancellationTokenSource();
        var requests = new List<Guid>();
        using var cancelled = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (request, _, _) => { requests.Add(Guid.Parse(request.Arguments.GetProperty("sessionId").GetString()!)); return Task.FromResult(ToolResult.Success("ok")); } }, EmitSource(state));
        cancelled.Attach(context, state, 1, scope.Token);
        cancelled.ResizeCommand.Execute(new(40, 10));
        var replacement = state with { SessionId = Guid.NewGuid(), Revision = 2 };
        cancelled.Observe(replacement);
        scope.Cancel();
        cancelled.ApplyCommand.Execute(null);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(requests);
    }, TestContext.Current.CancellationToken);
}
