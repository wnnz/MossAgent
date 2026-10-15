using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalPaneCompletionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);

    [Fact]
    public Task ReentrantTaskSwitchDuringOutputNotificationCannotLeakTruncationNotice() => _session.Dispatch(() =>
    {
        var source = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        var first = Context();
        var second = Context();
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        var switched = false;
        pane.PropertyChanged += (_, args) =>
        {
            if (switched || args.PropertyName != nameof(pane.Output) || pane.Output.Length == 0) return;
            switched = true;
            pane.AttachTask(second.TaskId, second.WorkingDirectory, second);
        };
        source.Emit(first.TaskId, new string('x', 210_000));
        Dispatcher.UIThread.RunJobs();
        Assert.True(switched);
        Assert.Empty(pane.Output);
        Assert.False(pane.HasTruncatedOutput);
        source.Emit(second.TaskId, "new task");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("new task", pane.Output);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelledRequestReturningSuccessDoesNotClaimTheSideEffectWasUndone() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pane = new TerminalPaneViewModel(new InspectorProbeTerminal(), new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "sent";
        var writing = pane.SendCommand.ExecuteAsync(null);
        try
        {
            pane.CancelOperationCommand.Execute(null);
            pending.SetResult(ToolResult.Success("already-written"));
            await writing;
            Assert.Empty(pane.Input);
            Assert.Contains("可能已生效", pane.Status);
            Assert.False(pane.IsBusy);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await writing; }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReentrantCancellationCompletionCannotRestoreCancellingState() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>();
        CancellationTokenRegistration registration = default;
        using var pane = new TerminalPaneViewModel(new InspectorProbeTerminal(), new InspectorProbeToolExecutor
        {
            Execute = (_, _, token) =>
            { registration = token.Register(() => pending.TrySetCanceled(token)); return pending.Task; }
        });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var starting = pane.StartCommand.ExecuteAsync(null);
        try
        {
            pane.CancelOperationCommand.Execute(null);
            await starting;
            Assert.False(pane.IsBusy);
            Assert.Contains("已取消", pane.Status);
            Assert.DoesNotContain("正在取消", pane.Status);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await starting; registration.Dispose(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposalCancelsOwnedOperationAndRejectsLateOutputAndResult() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var source = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor
        { Execute = (_, _, cancellation) => { token = cancellation; return pending.Task; } });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var starting = pane.StartCommand.ExecuteAsync(null);
        try
        {
            source.Emit(context.TaskId, "pending");
            pane.Dispose();
            Assert.True(token.IsCancellationRequested);
            Assert.False(starting.IsCompleted);
            Assert.False(pane.StartCommand.CanExecute(null));
            Assert.False(pane.StopCommand.CanExecute(null));
            Assert.False(pane.CancelOperationCommand.CanExecute(null));
            source.Emit(context.TaskId, "late");
            Dispatcher.UIThread.RunJobs();
            var status = pane.Status;
            pending.SetResult(ToolResult.Success("late"));
            await starting;
            Assert.Equal(status, pane.Status);
            Assert.Empty(pane.Output);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await starting; }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NewerInputSurvivesEarlierSuccessfulWriteAndUnicodeHistoryCutIsValid() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "first";
        var writing = pane.SendCommand.ExecuteAsync(null);
        try
        {
            pane.Input = "newer";
            pending.SetResult(ToolResult.Success("written"));
            await writing;
            Assert.Equal("newer", pane.Input);
            source.Emit(context.TaskId, "🚀");
            Dispatcher.UIThread.RunJobs();
            source.Emit(context.TaskId, new string('x', 199_999));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new string('x', 199_999), pane.Output);
            Assert.True(pane.HasTruncatedOutput);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await writing; }
    }, TestContext.Current.CancellationToken);
}
