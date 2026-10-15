using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalPaneOperationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);

    [Fact]
    public Task CancelKeepsBusyAndInputUntilNonCooperativePipelineReturns() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = CancellationToken.None;
        var terminal = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(terminal, new InspectorProbeToolExecutor
        { Execute = (_, _, token) => { captured = token; return pending.Task; } });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "retained";
        var starting = pane.StartCommand.ExecuteAsync(null);
        try
        {
            Assert.True(pane.IsBusy);
            Assert.True(pane.CancelOperationCommand.CanExecute(null));
            Assert.False(pane.StartCommand.CanExecute(null));
            Assert.False(pane.StopCommand.CanExecute(null));
            Assert.False(pane.CanEditInput);
            pane.CancelOperationCommand.Execute(null);
            Assert.True(captured.IsCancellationRequested);
            Assert.True(pane.IsBusy);
            Assert.False(pane.CancelOperationCommand.CanExecute(null));
            Assert.Equal("retained", pane.Input);
            Assert.Contains("等待清理", pane.Status);
            pending.SetResult(ToolResult.Failure("secret", "tool_cancelled"));
            await starting;
            Assert.False(pane.IsBusy);
            Assert.True(pane.CanEditInput);
            Assert.True(pane.StartCommand.CanExecute(null));
            Assert.Contains("已取消", pane.Status);
            Assert.DoesNotContain("secret", pane.Status);
            Assert.Equal(0, terminal.DirectCalls);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await starting; }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DifferentTaskCanRunWhileOldOperationRetainsCleanupOwnership() => _session.DispatchAsync(async () =>
    {
        var first = Context();
        var second = Context();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new List<CancellationToken>();
        using var pane = new TerminalPaneViewModel(new InspectorProbeTerminal(), new InspectorProbeToolExecutor
        {
            Execute = (_, context, token) =>
            { tokens.Add(token); return context.TaskId == first.TaskId ? pending.Task : Task.FromResult(ToolResult.Success("second")); }
        });
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        var old = pane.StartCommand.ExecuteAsync(null);
        try
        {
            pane.AttachTask(second.TaskId, second.WorkingDirectory, second);
            Assert.True(tokens[0].IsCancellationRequested);
            Assert.False(old.IsCompleted);
            Assert.False(pane.IsBusy);
            Assert.True(pane.StartCommand.CanExecute(null));
            await pane.StartCommand.ExecuteAsync(null);
            var status = pane.Status;
            Assert.Equal(2, tokens.Count);
            pane.Input = "newer draft";
            pending.SetResult(ToolResult.Failure("old-secret", "tool_failed"));
            await old;
            Assert.Equal(status, pane.Status);
            Assert.Equal("newer draft", pane.Input);
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await old; }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReturningToSameTaskCannotStartAReplacementBeforeOldOperationReturns() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var pane = new TerminalPaneViewModel(new InspectorProbeTerminal(), new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return pending.Task; } });
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var old = pane.StartCommand.ExecuteAsync(null);
        try
        {
            pane.AttachTask(null, null);
            pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
            Assert.True(pane.IsBusy);
            Assert.False(pane.StartCommand.CanExecute(null));
            await pane.StartCommand.ExecuteAsync(null);
            Assert.Equal(1, calls);
            pending.SetResult(ToolResult.Success("late success"));
            await old;
            Assert.False(pane.IsBusy);
            Assert.Contains("先前上下文", pane.Status);
            Assert.True(pane.StartCommand.CanExecute(null));
        }
        finally { pending.TrySetResult(ToolResult.Failure("end", "tool_cancelled")); await old; }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("approval_denied", "未获许可")]
    [InlineData("capability_denied", "未获许可")]
    [InlineData("path_denied", "授权范围")]
    [InlineData("tool_cancelled", "已取消")]
    [InlineData("tool_timeout", "超时")]
    [InlineData("unknown-secret-code", "未完成")]
    public Task PipelineErrorsUseLocalizedSafeHintsAndRetainDraft(string error, string expected) => _session.DispatchAsync(async () =>
    {
        using var pane = new TerminalPaneViewModel(new InspectorProbeTerminal(), new InspectorProbeToolExecutor
        { Execute = (_, _, _) => Task.FromResult(ToolResult.Failure("secret-api-key", error)) });
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "retained";
        await pane.SendCommand.ExecuteAsync(null);
        Assert.Equal("retained", pane.Input);
        Assert.Contains(expected, pane.Status);
        Assert.DoesNotContain("secret", pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OutputBurstPublishesOnceAndResetsTruncationWithTaskScope() => _session.Dispatch(() =>
    {
        var terminal = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(terminal, new InspectorProbeToolExecutor());
        var context = Context();
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var updates = 0;
        pane.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(pane.Output)) updates++; };
        for (var count = 0; count < 5000; count++) terminal.Emit(context.TaskId, new string('x', 100));
        Assert.Equal(0, updates);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, updates);
        Assert.Equal(200_000, pane.Output.Length);
        Assert.True(pane.HasTruncatedOutput);
        pane.AttachTask(null, null);
        Assert.False(pane.HasTruncatedOutput);
        Assert.Empty(pane.Output);
    }, TestContext.Current.CancellationToken);
}
