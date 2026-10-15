using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalStatePaneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static TerminalSessionSnapshot Snapshot(ToolExecutionContext context, long revision, TerminalSessionState state, int? exitCode = null) =>
        new(context.TaskId, Guid.NewGuid(), revision, state, exitCode);

    [Fact]
    public Task AttachReadsAlreadyRunningSessionWithoutCallingExecutionApis() => _session.Dispatch(() =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        source.Emit(Snapshot(context, 1, TerminalSessionState.Running));
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "draft";
        Assert.Contains("运行中", pane.SessionStatus);
        Assert.True(pane.HasSessionState);
        Assert.False(pane.HasOperationNotice);
        Assert.False(pane.StartCommand.CanExecute(null));
        Assert.True(pane.SendCommand.CanExecute(null));
        Assert.True(pane.StopCommand.CanExecute(null));
        Assert.Contains("没有终端输出", pane.EmptyTitle);
        Assert.Equal(0, source.DirectCalls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task IdleSessionAllowsDraftAndStartButNotSendOrStop() => _session.Dispatch(() =>
    {
        var context = Context();
        using var pane = new TerminalPaneViewModel(new TerminalStateProbe(), new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "draft";
        Assert.True(pane.StartCommand.CanExecute(null));
        Assert.True(pane.CanEditInput);
        Assert.False(pane.SendCommand.CanExecute(null));
        Assert.False(pane.StopCommand.CanExecute(null));
        Assert.Contains("尚未启动", pane.SessionStatus);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NaturalExitDisablesWritesPreservesDraftAndOperationNotice() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        source.Emit(Snapshot(context, 1, TerminalSessionState.Running));
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor
        { Execute = (_, _, _) => Task.FromResult(ToolResult.Failure("api-secret", "approval_denied")) });
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "draft";
        await pane.SendCommand.ExecuteAsync(null);
        var notice = pane.Status;
        source.Emit(Snapshot(context, 2, TerminalSessionState.Exited, 23));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("退出码 23", pane.SessionStatus);
        Assert.Equal("draft", pane.Input);
        Assert.Equal(notice, pane.Status);
        Assert.True(pane.HasOperationNotice);
        Assert.True(pane.StartCommand.CanExecute(null));
        Assert.False(pane.SendCommand.CanExecute(null));
        Assert.False(pane.StopCommand.CanExecute(null));
        Assert.Equal(0, source.DirectCalls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OutOfOrderAndOldTaskEventsCannotOverrideCurrentSnapshot() => _session.Dispatch(() =>
    {
        var first = Context();
        var second = Context();
        var source = new TerminalStateProbe();
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        source.Emit(Snapshot(first, 9, TerminalSessionState.Exited, 7));
        pane.AttachTask(second.TaskId, second.WorkingDirectory, second);
        source.Emit(Snapshot(first, 10, TerminalSessionState.Failed));
        source.Emit(Snapshot(second, 12, TerminalSessionState.Running));
        source.Emit(Snapshot(second, 11, TerminalSessionState.Exited, 91));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("运行中", pane.SessionStatus);
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        Assert.Contains("会话失败", pane.SessionStatus);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvocationRefreshesSnapshotBeforeDispatcherHasPublishedIt() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var calls = 0;
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("unused")); } });
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        source.Emit(Snapshot(context, 1, TerminalSessionState.Running));
        await pane.StartCommand.ExecuteAsync(null);
        Assert.Equal(0, calls);
        Assert.Contains("运行中", pane.SessionStatus);
        Assert.False(pane.StartCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ExitDuringPendingOperationDoesNotReleasePipelineOwnership() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task });
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var operation = pane.StartCommand.ExecuteAsync(null);
        try
        {
            source.Emit(Snapshot(context, 1, TerminalSessionState.Exited, 0));
            Dispatcher.UIThread.RunJobs();
            Assert.True(pane.IsBusy);
            Assert.False(pane.StartCommand.CanExecute(null));
            Assert.Contains("已退出", pane.SessionStatus);
            pending.SetResult(ToolResult.Success("started-and-exited"));
            await operation;
            Assert.False(pane.IsBusy);
            Assert.True(pane.StartCommand.CanExecute(null));
            Assert.Contains("已退出", pane.SessionStatus);
        }
        finally { pending.TrySetResult(ToolResult.Success("end")); await operation; }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(TerminalSessionState.Stopping, "清理终端资源")]
    [InlineData(TerminalSessionState.CleanupFailed, "清理失败")]
    public Task UnfinishedCleanupDoesNotAllowReplacementOrWrites(TerminalSessionState state, string label) => _session.Dispatch(() =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        source.Emit(Snapshot(context, 1, state));
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "draft";
        Assert.Contains(label, pane.SessionStatus);
        Assert.False(pane.StartCommand.CanExecute(null));
        Assert.False(pane.SendCommand.CanExecute(null));
        Assert.False(pane.StopCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedStateReadBlocksCommandsWithoutExposingException() => _session.Dispatch(() =>
    {
        var context = Context();
        var source = new TerminalStateProbe { Read = _ => throw new IOException("api-secret") };
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        Assert.False(pane.StartCommand.CanExecute(null));
        Assert.Contains("无法确认", pane.SessionStatus);
        Assert.DoesNotContain("secret", pane.SessionStatus);
        source.Read = null;
        source.Emit(Snapshot(context, 1, TerminalSessionState.Running));
        Dispatcher.UIThread.RunJobs();
        Assert.True(pane.StopCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposalRetiresStateSubscriptionAndRejectsQueuedNotification() => _session.Dispatch(() =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        Assert.Equal(1, source.StateSubscriptions);
        source.Emit(Snapshot(context, 1, TerminalSessionState.Running));
        pane.Dispose();
        Assert.Equal(0, source.StateSubscriptions);
        source.Emit(Snapshot(context, 2, TerminalSessionState.Exited, 0));
        Dispatcher.UIThread.RunJobs();
        Assert.False(pane.HasSessionState);
        Assert.False(pane.StartCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SameSnapshotRecoversReadFailureAndPublishesCommandEligibility() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var source = new TerminalStateProbe();
        var snapshot = Snapshot(context, 1, TerminalSessionState.Running);
        source.Emit(snapshot);
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var changes = 0;
        pane.StartCommand.CanExecuteChanged += (_, _) => changes++;
        source.Read = _ => throw new IOException("secret");
        await pane.StopCommand.ExecuteAsync(null);
        Assert.False(pane.StopCommand.CanExecute(null));
        Assert.True(changes > 0);
        source.Read = null;
        source.Emit(snapshot);
        Dispatcher.UIThread.RunJobs();
        Assert.True(pane.StopCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReentrantSelectionCannotLeaveOldStateInNewTask() => _session.Dispatch(() =>
    {
        var first = Context();
        var second = Context();
        var source = new TerminalStateProbe();
        using var pane = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        var changed = false;
        pane.PropertyChanged += (_, args) =>
        {
            if (changed || args.PropertyName != nameof(pane.SessionStatus)) return;
            changed = true;
            pane.AttachTask(second.TaskId, second.WorkingDirectory, second);
            pane.Input = "second draft";
        };
        source.Emit(Snapshot(first, 1, TerminalSessionState.Exited, 23));
        Dispatcher.UIThread.RunJobs();
        Assert.True(changed);
        Assert.Contains("尚未启动", pane.SessionStatus);
        Assert.Equal("second draft", pane.Input);
        Assert.True(pane.StartCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
