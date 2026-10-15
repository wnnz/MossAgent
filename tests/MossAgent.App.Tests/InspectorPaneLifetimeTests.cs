using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorPaneLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.ReadOnly);
    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read", ToolRiskLevel.ReadOnly,
        ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 0, 0);
    private static async Task DrainAsync()
    {
        for (var count = 0; count < 3; count++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
    }

    [Fact]
    public Task SlowStatusFromOldTask_DoesNotPopulateNewTaskOrUndoLoadingState() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        var reading = pane.RefreshCommand.ExecuteAsync(null);
        Assert.True(pane.IsLoading);
        pane.Attach(Context());
        pending.SetResult(ToolResult.Success("old", " M old.cs\0"));
        await reading;
        Assert.Empty(pane.Files);
        Assert.Empty(pane.Content);
        Assert.False(pane.IsLoading);
        Assert.True(pane.HasTask);
        pane.Attach(null);
        Assert.False(pane.RefreshCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SlowDiffAndStagedToggle_KeepOnlyTheMostRecentFileRequest() => _session.DispatchAsync(async () =>
    {
        var first = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => request.ToolName == "git.status"
                ? Task.FromResult(ToolResult.Success("status", "MM same.cs\0"))
                : request.Arguments.GetProperty("staged").GetBoolean()
                    ? Task.FromResult(ToolResult.Success("staged", "staged-content")) : first.Task
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.True(pane.IsDiffLoading);
        pane.IsStaged = true;
        Assert.Equal("staged-content", pane.Content);
        first.SetResult(ToolResult.Success("old", "old-unstaged-content"));
        await DrainAsync();
        Assert.Equal("staged-content", pane.Content);
        Assert.False(pane.IsDiffLoading);
        pane.Dispose();
        Assert.False(pane.RefreshCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AuditRefreshAndArtifacts_IgnoreOldTaskAndOldSelectionResults() => _session.DispatchAsync(async () =>
    {
        var old = new TaskCompletionSource<IReadOnlyList<ToolExecutionAuditRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldArtifacts = new TaskCompletionSource<IReadOnlyList<ToolArtifactRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = Guid.NewGuid();
        var secondTask = Guid.NewGuid();
        var first = Record(secondTask);
        var second = Record(secondTask);
        var reader = new InspectorProbeAuditReader
        {
            Recent = (task, _) => task == firstTask ? old.Task : Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([first, second]),
            Artifacts = (id, _) => id == first.Id ? oldArtifacts.Task : Task.FromResult<IReadOnlyList<ToolArtifactRecord>>([])
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(firstTask);
        pane.Attach(secondTask);
        Assert.Equal(2, pane.Executions.Count);
        pane.SelectedExecution = second;
        old.SetResult([Record(firstTask)]);
        oldArtifacts.SetResult([new ToolArtifactRecord(Guid.NewGuid(), first.Id, secondTask, "old", "old.png", "image/png", 1, "hash", DateTimeOffset.UtcNow)]);
        await DrainAsync();
        Assert.Equal(second, pane.SelectedExecution);
        Assert.All(pane.Executions, record => Assert.Equal(secondTask, record.TaskId));
        Assert.Empty(pane.Artifacts);
        pane.Attach(null);
        Assert.Empty(pane.Executions);
        Assert.False(pane.RefreshCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AuditFailure_IsActionableAndDoesNotLeakReaderException() => _session.DispatchAsync(async () =>
    {
        var reader = new InspectorProbeAuditReader
        { Recent = (_, _) => Task.FromException<IReadOnlyList<ToolExecutionAuditRecord>>(new IOException("secret-api-key")) };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(Guid.NewGuid());
        await DrainAsync();
        Assert.Contains("刷新重试", pane.Status);
        Assert.DoesNotContain("secret", pane.Status);
        Assert.False(pane.IsLoading);
        Assert.True(pane.RefreshCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TerminalActions_UsePipelineAndRetainRefusedInput() => _session.DispatchAsync(async () =>
    {
        var terminals = new InspectorProbeTerminal();
        var requests = new List<ToolRequest>();
        var context = Context();
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, captured, _) =>
            { Assert.Same(context, captured); requests.Add(request); return Task.FromResult(ToolResult.Failure("secret", "approval-denied")); }
        };
        using var pane = new TerminalPaneViewModel(terminals, executor);
        pane.AttachTask(context.TaskId, context.WorkingDirectory, context);
        pane.Input = "synthetic-command";
        await pane.SendCommand.ExecuteAsync(null);
        Assert.Equal("terminal.write", Assert.Single(requests).ToolName);
        Assert.Equal("synthetic-command\r", requests[0].Arguments.GetProperty("input").GetString());
        Assert.Equal("synthetic-command", pane.Input);
        Assert.DoesNotContain("secret", pane.Status);
        Assert.Equal(0, terminals.DirectCalls);
        pane.AttachTask(null, null);
        Assert.False(pane.StartCommand.CanExecute(null));
        Assert.False(pane.SendCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task QueuedTerminalOutput_DoesNotCrossTaskOrDisposalBoundary() => _session.Dispatch(() =>
    {
        var terminals = new InspectorProbeTerminal();
        using var pane = new TerminalPaneViewModel(terminals, new InspectorProbeToolExecutor());
        var first = Context();
        var second = Context();
        pane.AttachTask(first.TaskId, first.WorkingDirectory, first);
        terminals.Emit(first.TaskId, "old queued output");
        pane.AttachTask(second.TaskId, second.WorkingDirectory, second);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(pane.Output);
        terminals.Emit(second.TaskId, new string('x', 220_000));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(200_000, pane.Output.Length);
        pane.Dispose();
        terminals.Emit(second.TaskId, "after disposal");
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(pane.Output);
    }, TestContext.Current.CancellationToken);
}
