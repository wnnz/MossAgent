using Avalonia.Headless;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalInputOwnershipTests : IAsyncLifetime
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
    public Task SwitchingScopeCancelsQueueButRetainsOldOwnerUntilReturn() => _session.DispatchAsync(async () =>
    {
        var first = Context();
        var second = Context();
        var source = new TerminalStateProbe();
        var firstFrame = Frame(first, source);
        var secondFrame = Frame(second, source);
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<ToolRequest>();
        var token = CancellationToken.None;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        {
            Execute = (request, context, cancellation) =>
            {
                requests.Add(request);
                if (context.TaskId == first.TaskId && requests.Count == 1) { token = cancellation; return pending.Task; }
                return Task.FromResult(ToolResult.Success("ok"));
            }
        }, source);
        try
        {
            keyboard.Attach(first, 1, default);
            keyboard.Observe(firstFrame, true);
            keyboard.InputCommand.Execute(new(firstFrame.ViewId, firstFrame.SessionId!.Value, "first"));
            await WaitAsync(() => requests.Count == 1);
            keyboard.InputCommand.Execute(new(firstFrame.ViewId, firstFrame.SessionId.Value, "discard-old"));
            keyboard.Attach(second, 2, default);
            keyboard.Observe(secondFrame, true);
            Assert.True(token.IsCancellationRequested);
            keyboard.InputCommand.Execute(new(secondFrame.ViewId, secondFrame.SessionId!.Value, "independent"));
            await WaitAsync(() => requests.Count == 2 && !keyboard.IsBusy);
            keyboard.Attach(first, 3, default);
            keyboard.Observe(firstFrame, true);
            Assert.True(keyboard.IsBusy);
            Assert.False(keyboard.CanInput);
            Assert.Contains("等待先前", keyboard.Notice);
            keyboard.InputCommand.Execute(new(firstFrame.ViewId, firstFrame.SessionId.Value, "not-admitted"));
            pending.SetResult(ToolResult.Failure("late-secret", "tool_failed"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.True(keyboard.CanInput);
            Assert.False(keyboard.HasNotice);
            keyboard.InputCommand.Execute(new(firstFrame.ViewId, firstFrame.SessionId.Value, "new"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.Equal(new[] { "first", "independent", "new" }, requests.Select(request => request.Arguments.GetProperty("input").GetString()));
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("replace-session")]
    [InlineData("disable")]
    public Task CancellationDoesNotReleaseNonCooperativeWriteOrSendRemainder(string action) => _session.DispatchAsync(async () =>
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
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "started"));
        await WaitAsync(() => calls == 1);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "queued"));
        if (action == "cancel") keyboard.CancelCommand.Execute(null);
        if (action == "dispose") keyboard.Dispose();
        if (action == "replace-session") keyboard.Observe(frame with { SessionId = Guid.NewGuid(), ViewId = Guid.NewGuid() }, true);
        if (action == "disable") keyboard.Observe(frame, false);
        Assert.True(token.IsCancellationRequested);
        if (action != "dispose") Assert.True(keyboard.IsBusy);
        Assert.False(keyboard.CanInput);
        pending.SetResult(ToolResult.Success("already started"));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.Equal(1, calls);
    }, TestContext.Current.CancellationToken);
}
