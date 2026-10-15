using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditPaneSelectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read",
        ToolRiskLevel.ReadOnly, ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 1, 0);

    [Fact]
    public Task RefreshKeepsTheSelectedExecutionInsteadOfSelectingTheFirstRow() => _session.DispatchAsync(async () =>
    {
        var taskId = Guid.NewGuid();
        var first = Record(taskId);
        var second = Record(taskId);
        var reader = new InspectorProbeAuditReader { Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([first, second]) };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        pane.SelectedExecution = second;
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.Same(second, pane.SelectedExecution);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EqualRefreshedRecordsRebindTheCurrentRowAndSelection() => _session.DispatchAsync(async () =>
    {
        var original = Record(Guid.NewGuid());
        var replacement = original with { };
        var calls = 0;
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([++calls == 1 ? original : replacement])
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(original.TaskId);
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.Same(replacement, pane.SelectedExecution);
        Assert.Same(replacement, pane.SelectedItem!.Record);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PendingRefreshKeepsTheUsersMoreRecentSelection() => _session.DispatchAsync(async () =>
    {
        var taskId = Guid.NewGuid();
        var first = Record(taskId);
        var second = Record(taskId);
        var pending = new TaskCompletionSource<IReadOnlyList<ToolExecutionAuditRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => ++calls == 1 ? Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([first, second]) : pending.Task
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        var refresh = pane.RefreshCommand.ExecuteAsync(null);
        pane.SelectedExecution = second;
        pending.SetResult([first with { }, second with { }]);
        await refresh;
        Assert.Equal(second.Id, pane.SelectedExecution!.Id);
        Assert.Same(pane.SelectedExecution, pane.SelectedItem!.Record);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HistoryFiltersForeignTasksAndCapsEvenAnOversizedPortResponse() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var records = Enumerable.Range(0, 240).Select(_ => Record(taskId)).Prepend(Record(Guid.NewGuid())).ToArray();
        var reader = new InspectorProbeAuditReader { Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>(records) };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        Assert.Equal(200, pane.Executions.Count);
        Assert.All(pane.Executions, record => Assert.Equal(taskId, record.TaskId));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UnlistedSelectionCannotReadArtifactsOrReplaceTheCurrentSelection() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var current = Record(taskId);
        var reads = new List<Guid>();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([current]),
            Artifacts = (id, _) => { reads.Add(id); return Task.FromResult<IReadOnlyList<ToolArtifactRecord>>([]); }
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        pane.SelectedExecution = Record(taskId);
        Assert.Same(current, pane.SelectedExecution);
        Assert.Equal(current.Id, Assert.Single(reads));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArtifactsRejectForeignOwnershipAndCapVisibleRows() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var current = Record(taskId);
        var rows = Enumerable.Range(0, 60).Select(index => new ToolArtifactRecord(Guid.NewGuid(), current.Id, taskId,
            $"file-{index}", $"file-{index}.txt", "text/plain", 1, "hash", DateTimeOffset.UtcNow))
            .Prepend(new ToolArtifactRecord(Guid.NewGuid(), Guid.NewGuid(), taskId, "foreign", "foreign", "text/plain", 1, "hash", DateTimeOffset.UtcNow))
            .ToArray();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([current]),
            Artifacts = (_, _) => Task.FromResult<IReadOnlyList<ToolArtifactRecord>>(rows)
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(50, pane.Artifacts.Count);
        Assert.All(pane.Artifacts, artifact => Assert.Equal(current.Id, artifact.ExecutionId));
    }, TestContext.Current.CancellationToken);
}
