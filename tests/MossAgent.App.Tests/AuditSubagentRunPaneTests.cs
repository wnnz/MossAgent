using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditSubagentRunPaneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private const string Snapshot = """{"providerProtocol":"openai-responses","modelId":"gpt-5","reasoningEffort":"high","shareContext":true,"taskPreview":"重写认证模块"}""";

    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read",
        ToolRiskLevel.ReadOnly, ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 1, 0);

    private static SubagentRunRecord Run(Guid taskId, SubagentRunStatus status = SubagentRunStatus.Completed) =>
        new(Guid.NewGuid(), taskId, "call", "coder", Snapshot, status, null,
            DateTimeOffset.UtcNow.AddSeconds(-2), DateTimeOffset.UtcNow);

    [Fact]
    public Task AttachLoadsJournaledRunsBesideExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var run = Run(taskId, SubagentRunStatus.Failed);
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>([run])
        };
        using var pane = new AuditPaneViewModel(reader, journal);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SubagentRuns);
        Assert.Equal(run.RunId, item.Record.RunId);
        Assert.Equal("failed", item.OutcomeKind);
        Assert.True(pane.HasRuns);
        Assert.Equal(InspectorText.Format("AuditSubagentRunsTitle", 1), pane.RunsTitle);
        Assert.Same(item, pane.SelectedSubagentRun);
        Assert.Single(pane.Executions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingJournalLeavesRunsEmptyWithoutAffectingExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        Assert.Empty(pane.SubagentRuns);
        Assert.False(pane.HasRuns);
        Assert.Single(pane.Executions);
        Assert.True(pane.HasExecutions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task JournalFailureLeavesRunsEmptyWhileExecutionsStillLoad() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromException<IReadOnlyList<SubagentRunRecord>>(new InvalidOperationException("storage"))
        };
        using var pane = new AuditPaneViewModel(reader, journal);
        pane.Attach(taskId);
        Assert.Empty(pane.SubagentRuns);
        Assert.False(pane.HasRuns);
        Assert.Single(pane.Executions);
        Assert.Equal(InspectorText.Format("AuditCount", 1), pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RunsRejectForeignTaskRowsLikeTheExecutionHistory() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>(
                [Run(taskId), Run(Guid.NewGuid()), Run(taskId)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), journal);
        pane.Attach(taskId);
        Assert.Equal(2, pane.SubagentRuns.Count);
        Assert.All(pane.SubagentRuns, run => Assert.Equal(taskId, run.Record.TaskId));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RunSelectionStaysIndependentFromTheExecutionSelection() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var execution = Record(taskId);
        var runs = new[] { Run(taskId), Run(taskId, SubagentRunStatus.Running) };
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([execution])
        };
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>(runs)
        };
        using var pane = new AuditPaneViewModel(reader, journal);
        pane.Attach(taskId);
        pane.SelectedSubagentRun = pane.SubagentRuns[1];
        Assert.Same(execution, pane.SelectedExecution);
        Assert.Same(pane.SubagentRuns[1], pane.SelectedSubagentRun);
        var foreign = new AuditSubagentRunViewModel(Run(taskId));
        pane.SelectedSubagentRun = foreign;
        Assert.NotSame(foreign, pane.SelectedSubagentRun);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RefreshKeepsTheSelectedRunByRunId() => _session.Dispatch(async () =>
    {
        var taskId = Guid.NewGuid();
        var keep = Run(taskId, SubagentRunStatus.Cancelled);
        var calls = 0;
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>(
                ++calls == 1 ? [Run(taskId), keep] : [keep with { }, Run(taskId)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), journal);
        pane.Attach(taskId);
        pane.SelectedSubagentRun = pane.SubagentRuns.Single(run => run.Record.RunId == keep.RunId);
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(keep.RunId, pane.SelectedSubagentRun!.Record.RunId);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachingAnotherTaskClearsTheRunProjection() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>([Run(taskId)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), journal);
        pane.Attach(taskId);
        Assert.True(pane.HasRuns);
        pane.Attach(null);
        Assert.Empty(pane.SubagentRuns);
        Assert.False(pane.HasRuns);
        Assert.Null(pane.SelectedSubagentRun);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompositionRootInjectsTheRegisteredRunJournal() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new InspectorProbeSubagentRunJournal
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>([Run(taskId)])
        };
        var provider = new ServiceCollection()
            .AddSingleton<IToolExecutionAuditReader>(new InspectorProbeAuditReader())
            .AddSingleton<ISubagentRunJournal>(journal)
            .AddSingleton<AuditPaneViewModel>()
            .BuildServiceProvider();
        using var pane = provider.GetRequiredService<AuditPaneViewModel>();
        pane.Attach(taskId);
        Assert.True(pane.HasRuns);
        Assert.Single(pane.SubagentRuns);
    }, TestContext.Current.CancellationToken);
}
