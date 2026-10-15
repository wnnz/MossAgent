using Avalonia.Headless;
using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalPasteOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static TerminalFrame Frame(ToolExecutionContext context, TerminalStateProbe states)
    {
        using var screen = new TerminalVtScreen();
        var frame = screen.Capture(Guid.NewGuid(), context.TaskId, Guid.NewGuid(), false);
        states.Emit(new(context.TaskId, frame.SessionId!.Value, 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default));
        return frame;
    }

    private static async Task WaitAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    [Fact]
    public Task PasteIsExclusiveAndNeverMergesWithTypingOrLegacyInput() => _session.DispatchAsync(async () =>
    {
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        var states = new TerminalStateProbe();
        var frame = Frame(context, states);
        var requests = new List<ToolRequest>();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (request, _, _) => { requests.Add(request); return pending.Task; } }, states);
        keyboard.Attach(context, 7, default);
        keyboard.Observe(frame, true);
        try
        {
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "paste", IsPaste: true, ScopeGeneration: 7));
            Assert.True(keyboard.IsBusy);
            Assert.False(keyboard.CanInput);
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId.Value, "must-not-merge"));
            await WaitAsync(() => requests.Count == 1);
            Assert.Equal("paste", requests[0].Arguments.GetProperty("input").GetString());
            pending.SetResult(ToolResult.Success("ok"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.True(keyboard.CanInput);
            Assert.Single(requests);
            Assert.Equal(0, states.DirectCalls);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("approval_denied")]
    [InlineData("tool_cancelled")]
    [InlineData("path_denied")]
    public Task RefusedPasteIsNotReplayedWhenResumed(string error) => _session.DispatchAsync(async () =>
    {
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.AskEveryTime);
        var states = new TerminalStateProbe();
        var frame = Frame(context, states);
        var calls = 0;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Failure("secret", error)); } }, states);
        keyboard.Attach(context, 1, default);
        keyboard.Observe(frame, true);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "paste-secret", IsPaste: true));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.Equal(1, calls);
        Assert.False(keyboard.CanInput);
        Assert.DoesNotContain("secret", keyboard.Notice);
        keyboard.ResumeCommand.Execute(null);
        await Task.Delay(40, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangedInputModeCancelsInFlightPasteWithoutReencoding() => _session.DispatchAsync(async () =>
    {
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        var states = new TerminalStateProbe();
        var frame = Frame(context, states);
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var started = false;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (_, _, cancellation) => { started = true; token = cancellation; return pending.Task; } }, states);
        keyboard.Attach(context, 1, default);
        keyboard.Observe(frame, true);
        try
        {
            keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "paste", IsPaste: true));
            await WaitAsync(() => started);
            keyboard.Observe(frame with { BracketedPaste = true }, true);
            Assert.True(token.IsCancellationRequested);
            Assert.True(keyboard.IsBusy);
            Assert.False(keyboard.CanInput);
            pending.SetResult(ToolResult.Success("late"));
            await WaitAsync(() => !keyboard.IsBusy);
            Assert.False(keyboard.CanInput);
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ReadOnlyAndStaleScopeCannotAdmitPaste(bool readOnly) => _session.DispatchAsync(async () =>
    {
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], readOnly ? ApprovalPolicy.ReadOnly : ApprovalPolicy.FullAccess);
        var states = new TerminalStateProbe();
        var frame = Frame(context, states);
        var calls = 0;
        using var keyboard = new TerminalInputController(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } }, states);
        keyboard.Attach(context, 2, default);
        keyboard.Observe(frame, true);
        keyboard.InputCommand.Execute(new(frame.ViewId, frame.SessionId!.Value, "paste", IsPaste: true, ScopeGeneration: 1));
        await WaitAsync(() => !keyboard.IsBusy);
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);
}
