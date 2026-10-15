using Avalonia.Headless;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalResizeControllerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static TerminalSessionSnapshot State(ToolExecutionContext context) =>
        new(context.TaskId, Guid.NewGuid(), 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default);
    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    [Fact]
    public Task DebounceAndBusyUpdatesSendOnlyLatestDimensions() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = new TerminalStateProbe();
        source.Emit(state);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        {
            Execute = (request, captured, _) =>
            {
                Assert.Same(context, captured);
                requests.Add(request);
                return requests.Count == 1 ? pending.Task : Task.FromResult(ToolResult.Success("ok"));
            }
        }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        resize.ResizeCommand.Execute(new(41, 11));
        Assert.True(resize.IsBusy);
        Assert.False(resize.HasNotice);
        await WaitAsync(() => requests.Count == 1);
        Assert.Equal(41, requests[0].Arguments.GetProperty("columns").GetInt32());
        Assert.Equal(state.SessionId.ToString("D"), requests[0].Arguments.GetProperty("sessionId").GetString());
        resize.ResizeCommand.Execute(new(42, 12));
        resize.ResizeCommand.Execute(new(43, 13));
        pending.SetResult(ToolResult.Success("ok"));
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(2, requests.Count);
        Assert.Equal(43, requests[1].Arguments.GetProperty("columns").GetInt32());
        Assert.Equal(13, requests[1].Arguments.GetProperty("rows").GetInt32());
        Assert.Equal(0, source.DirectCalls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RefusalBlocksAutomaticRequestsUntilExplicitRetry() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = new TerminalStateProbe();
        source.Emit(state);
        var calls = 0;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        {
            Execute = (_, _, _) => Task.FromResult(++calls == 1 ? ToolResult.Failure("secret", "approval_denied") : ToolResult.Success("ok"))
        }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        await WaitAsync(() => !resize.IsBusy);
        Assert.Contains("许可", resize.Notice);
        Assert.DoesNotContain("secret", resize.Notice);
        resize.ResizeCommand.Execute(new(50, 15));
        resize.Observe(state with { Revision = 2 });
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        Assert.True(resize.RetryCommand.CanExecute(null));
        resize.RetryCommand.Execute(null);
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(2, calls);
        Assert.False(resize.HasNotice);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("readonly")]
    [InlineData("missing-grid")]
    [InlineData("wrong-task")]
    [InlineData("cancelled")]
    [InlineData("stopped")]
    public Task IneligibleScopesNeverExecute(string scenario) => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        using var scope = new CancellationTokenSource();
        if (scenario == "readonly") context = context with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        if (scenario == "missing-grid") state = state with { Grid = null };
        if (scenario == "wrong-task") state = state with { TaskId = Guid.NewGuid() };
        if (scenario == "cancelled") scope.Cancel();
        if (scenario == "stopped") state = state with { State = TerminalSessionState.Stopped };
        var calls = 0;
        var source = new TerminalStateProbe();
        source.Emit(state);
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, source);
        resize.Attach(context, state, 1, scope.Token);
        Assert.False(resize.ResizeCommand.CanExecute(new(40, 10)));
        resize.ResizeCommand.Execute(new(40, 10));
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NonCooperativeOwnerSurvivesCancelSwitchAndReturn() => _session.DispatchAsync(async () =>
    {
        var first = Context();
        var second = Context();
        var firstState = State(first);
        var secondState = State(second);
        var source = new TerminalStateProbe();
        source.Emit(firstState);
        source.Emit(secondState);
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<Guid>();
        var token = CancellationToken.None;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        {
            Execute = (_, context, cancellation) =>
            {
                requests.Add(context.TaskId);
                if (context.TaskId != first.TaskId || requests.Count > 2) return Task.FromResult(ToolResult.Success("ok"));
                token = cancellation;
                return pending.Task;
            }
        }, source);
        try
        {
            resize.Attach(first, firstState, 1, default);
            resize.ResizeCommand.Execute(new(40, 10));
            await WaitAsync(() => requests.Count == 1);
            resize.CancelCommand.Execute(null);
            Assert.True(token.IsCancellationRequested);
            Assert.True(resize.IsBusy);
            resize.Attach(second, secondState, 2, default);
            resize.ResizeCommand.Execute(new(50, 15));
            await WaitAsync(() => requests.Count == 2 && !resize.IsBusy);
            resize.Attach(first, firstState, 3, default);
            resize.ResizeCommand.Execute(new(60, 20));
            Assert.True(resize.IsBusy);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            Assert.Equal(2, requests.Count);
            pending.SetResult(ToolResult.Failure("late-secret", "tool_failed"));
            await WaitAsync(() => requests.Count == 3 && !resize.IsBusy);
            Assert.False(resize.HasNotice);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReplacedSessionBeforeDebounceDoesNotReachExecutor() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = new TerminalStateProbe();
        source.Emit(state);
        var calls = 0;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        source.Emit(state with { SessionId = Guid.NewGuid(), Revision = 2 });
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AlreadyAppliedGeometryDoesNotAskAgainWhenUiSnapshotLags() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var state = State(context);
        var source = new TerminalStateProbe();
        source.Emit(state with { Revision = 2, Grid = new(40, 10) });
        var calls = 0;
        using var resize = new TerminalResizeController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, source);
        resize.Attach(context, state, 1, default);
        resize.ResizeCommand.Execute(new(40, 10));
        await WaitAsync(() => !resize.IsBusy);
        Assert.Equal(0, calls);
        Assert.False(resize.HasNotice);
    }, TestContext.Current.CancellationToken);
}
