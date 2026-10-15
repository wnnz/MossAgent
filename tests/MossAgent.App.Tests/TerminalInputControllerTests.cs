using Avalonia.Headless;
using Avalonia.Input;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalInputControllerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static TerminalFrame Frame(ToolExecutionContext context, TerminalStateProbe source)
    {
        using var screen = new TerminalVtScreen();
        var session = Guid.NewGuid();
        source.Emit(new(context.TaskId, session, 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default));
        return screen.Capture(Guid.NewGuid(), context.TaskId, session, false);
    }
    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    [Fact]
    public Task InputDuringApprovalIsSerialAndNotSilentlyLost() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var frame = Frame(context, source);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        {
            Execute = (request, captured, _) =>
            {
                Assert.Same(context, captured);
                requests.Add(request);
                return requests.Count == 1 ? pending.Task : Task.FromResult(ToolResult.Success("ok"));
            }
        }, source);
        keyboard.Attach(context, 1, default);
        keyboard.Observe(frame, true);
        try
        {
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "first"));
            await WaitAsync(() => requests.Count == 1);
            Assert.False(keyboard.HasNotice);
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "红色"));
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, Key: Key.Enter));
            Assert.Single(requests);
            pending.SetResult(ToolResult.Success("ok"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.Equal(2, requests.Count);
            Assert.Equal("first", requests[0].Arguments.GetProperty("input").GetString());
            Assert.Equal("红色\r", requests[1].Arguments.GetProperty("input").GetString());
            Assert.All(requests, request =>
            {
                Assert.Equal("terminal.write", request.ToolName);
                Assert.Equal(frame.SessionId.Value.ToString("D"), request.Arguments.GetProperty("sessionId").GetString());
            });
            Assert.Equal(0, source.DirectCalls);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("approval_denied")]
    [InlineData("path_denied")]
    [InlineData("unknown-secret")]
    public Task FailureDiscardsQueueAndResumeNeverReplaysInput(string code) => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var frame = Frame(context, source);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => { requests.Add(request); return requests.Count == 1 ? pending.Task : Task.FromResult(ToolResult.Success("ok")); }
        }, source);
        keyboard.Attach(context, 1, default);
        keyboard.Observe(frame, true);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "first"));
        await WaitAsync(() => requests.Count == 1);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "never-replay"));
        pending.SetResult(ToolResult.Failure("secret", code));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.False(keyboard.CanInput);
        Assert.DoesNotContain("secret", keyboard.Notice);
        Assert.True(keyboard.ResumeCommand.CanExecute(null));
        keyboard.ResumeCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Single(requests);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "new"));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.Equal("new", requests[1].Arguments.GetProperty("input").GetString());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OverflowCancelsOwnerAndCannotReplayAcceptedRemainder() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var frame = Frame(context, source);
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var token = CancellationToken.None;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (_, _, cancellation) => { calls++; token = cancellation; return pending.Task; } }, source);
        keyboard.Attach(context, 1, default);
        keyboard.Observe(frame, true);
        try
        {
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, new string('x', 32768)));
            await WaitAsync(() => calls == 1);
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "overflow"));
            Assert.True(token.IsCancellationRequested);
            Assert.True(keyboard.IsBusy);
            Assert.Contains("队列已满", keyboard.Notice);
            Assert.False(keyboard.CanInput);
            pending.SetResult(ToolResult.Success("started write cannot be undone"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.Equal(1, calls);
            Assert.Contains("队列已满", keyboard.Notice);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("readonly")]
    [InlineData("cancelled")]
    [InlineData("failed-frame")]
    [InlineData("old-view")]
    [InlineData("disabled")]
    [InlineData("changed-session")]
    public Task InvalidScopesAndStaleFramesCannotSend(string scenario) => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var frame = Frame(context, source);
        using var scope = new CancellationTokenSource();
        if (scenario == "readonly") context = context with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        if (scenario == "cancelled") scope.Cancel();
        var calls = 0;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, source);
        keyboard.Attach(context, 1, scope.Token);
        keyboard.Observe(scenario == "failed-frame" ? frame with { Failed = true } : frame, scenario != "disabled");
        if (scenario == "changed-session") source.Emit(source.GetSnapshot(context.TaskId)! with { SessionId = Guid.NewGuid(), Revision = 2 });
        keyboard.InputCommand.Execute(new(scenario == "old-view" ? Guid.NewGuid() : frame.ViewId, frame.SessionId!.Value, "not-sent"));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);
}
