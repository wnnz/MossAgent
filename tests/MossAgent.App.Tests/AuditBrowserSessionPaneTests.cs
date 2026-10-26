using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditBrowserSessionPaneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read",
        ToolRiskLevel.ReadOnly, ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 1, 0);

    private static BrowserSessionRecord Session(Guid taskId,
        BrowserSessionState state = BrowserSessionState.Detached) =>
        new(Guid.NewGuid(), taskId, BrowserEngine.Chrome, BrowserProfileMode.DailyProfile, state,
            DateTimeOffset.UtcNow.AddSeconds(-2), DateTimeOffset.UtcNow);

    private static BrowserActionIntentRecord Intent(BrowserSessionRecord session,
        BrowserActionKind kind = BrowserActionKind.Click, string? targetHash = "a1b2c3d4e5f6a1b2",
        BrowserActionIntentState state = BrowserActionIntentState.Resolved) =>
        new(Guid.NewGuid(), session.SessionId, kind, targetHash, state,
            session.CreatedAt, DateTimeOffset.UtcNow);

    [Fact]
    public Task AttachLoadsJournaledSessionsWithTheirActionIntents() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var session = Session(taskId);
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([session]),
            IntentReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserActionIntentRecord>>(
                [Intent(session), Intent(session, BrowserActionKind.NavigateBack, null)])
        };
        using var pane = new AuditPaneViewModel(reader, browserJournal: journal);
        pane.Attach(taskId);

        var item = Assert.Single(pane.BrowserSessions);
        Assert.Equal(session.SessionId, item.Record.SessionId);
        Assert.Equal("success", item.OutcomeKind);
        Assert.Equal(InspectorText.Get("AuditBrowserSessionDetached"), item.OutcomeLabel);
        Assert.Equal(InspectorText.Format("AuditBrowserSessionSummary",
            InspectorText.Get("AuditBrowserEngineChrome"),
            InspectorText.Get("AuditBrowserProfileDaily")), item.Title);
        Assert.Equal(2, item.Actions.Count);
        Assert.Equal(InspectorText.Get("AuditBrowserActionClick"), item.Actions[0].KindLabel);
        Assert.Equal("success", item.Actions[0].OutcomeKind);
        Assert.Equal(InspectorText.Format("AuditBrowserTarget", "a1b2c3d4e5f6a1b2"), item.Actions[0].TargetLabel);
        Assert.Equal(InspectorText.Get("AuditBrowserActionNavigateBack"), item.Actions[1].KindLabel);
        Assert.Equal(string.Empty, item.Actions[1].TargetLabel);
        Assert.True(pane.HasBrowserSessions);
        Assert.Equal(InspectorText.Format("AuditBrowserSessionsTitle", 1), pane.BrowserSessionsTitle);
        Assert.Single(pane.Executions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InterruptedSessionSurfacesTheManualContinuationWarning() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>(
                [Session(taskId, BrowserSessionState.Interrupted)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), browserJournal: journal);
        pane.Attach(taskId);

        var item = Assert.Single(pane.BrowserSessions);
        Assert.Equal("interrupted", item.OutcomeKind);
        Assert.Equal(InspectorText.Get("AuditInterrupted"), item.OutcomeLabel);
        Assert.Equal(InspectorText.Get("AuditBrowserSessionInterruptedHint"), item.Detail);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CommittedIntentSurfacesThePendingWarningAndIsNeverReplayed() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var session = Session(taskId, BrowserSessionState.Interrupted);
        var pending = Intent(session, BrowserActionKind.Navigate,
            state: BrowserActionIntentState.Interrupted);
        var committed = Intent(session, BrowserActionKind.Type,
            state: BrowserActionIntentState.Committed);
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([session]),
            IntentReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserActionIntentRecord>>(
                [pending, committed])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), browserJournal: journal);
        pane.Attach(taskId);

        var item = Assert.Single(pane.BrowserSessions);
        // The interrupted-session warning wins over the per-intent pending hint.
        Assert.Equal(InspectorText.Get("AuditBrowserSessionInterruptedHint"), item.Detail);
        Assert.True(item.HasPendingAction);
        Assert.True(item.Actions[1].IsPending);
        Assert.Equal("running", item.Actions[1].OutcomeKind);
        Assert.Equal(InspectorText.Get("Pending"), item.Actions[1].OutcomeLabel);
        Assert.Equal(InspectorText.Get("AuditInterrupted"), item.Actions[0].OutcomeLabel);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachedSessionWithCommittedIntent_ShowsPendingWarning() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var session = Session(taskId, BrowserSessionState.Attached);
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([session]),
            IntentReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserActionIntentRecord>>(
                [Intent(session, state: BrowserActionIntentState.Committed)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), browserJournal: journal);
        pane.Attach(taskId);

        var item = Assert.Single(pane.BrowserSessions);
        Assert.Equal("running", item.OutcomeKind);
        Assert.Equal(InspectorText.Get("AuditBrowserSessionAttached"), item.OutcomeLabel);
        Assert.Equal(InspectorText.Get("AuditBrowserPendingIntentHint"), item.Detail);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingJournalLeavesSessionsEmptyWithoutAffectingExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        using var pane = new AuditPaneViewModel(reader);
        pane.Attach(taskId);
        Assert.Empty(pane.BrowserSessions);
        Assert.False(pane.HasBrowserSessions);
        Assert.Single(pane.Executions);
        Assert.True(pane.HasExecutions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SessionReadFailureLeavesSessionsEmptyWhileExecutionsStillLoad() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromException<IReadOnlyList<BrowserSessionRecord>>(
                new InvalidOperationException("storage"))
        };
        using var pane = new AuditPaneViewModel(reader, browserJournal: journal);
        pane.Attach(taskId);
        Assert.Empty(pane.BrowserSessions);
        Assert.False(pane.HasBrowserSessions);
        Assert.Single(pane.Executions);
        Assert.Equal(InspectorText.Format("AuditCount", 1), pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task IntentReadFailureLeavesSessionsEmptyWhileExecutionsStillLoad() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var reader = new InspectorProbeAuditReader
        {
            Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
        };
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([Session(taskId)]),
            IntentReads = (_, _) => Task.FromException<IReadOnlyList<BrowserActionIntentRecord>>(
                new InvalidOperationException("storage"))
        };
        using var pane = new AuditPaneViewModel(reader, browserJournal: journal);
        pane.Attach(taskId);
        Assert.Empty(pane.BrowserSessions);
        Assert.False(pane.HasBrowserSessions);
        Assert.Single(pane.Executions);
        Assert.Equal(InspectorText.Format("AuditCount", 1), pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SessionsRejectForeignTaskRows() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>(
                [Session(taskId), Session(Guid.NewGuid()), Session(taskId)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), browserJournal: journal);
        pane.Attach(taskId);
        Assert.Equal(2, pane.BrowserSessions.Count);
        Assert.All(pane.BrowserSessions, item => Assert.Equal(taskId, item.Record.TaskId));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachingAnotherTaskClearsTheSessionProjection() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([Session(taskId)])
        };
        using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), browserJournal: journal);
        pane.Attach(taskId);
        Assert.True(pane.HasBrowserSessions);
        pane.Attach(null);
        Assert.Empty(pane.BrowserSessions);
        Assert.False(pane.HasBrowserSessions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompositionRootInjectsTheRegisteredBrowserJournal() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var journal = new RecordingBrowserSessionJournal
        {
            SessionReads = (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([Session(taskId)])
        };
        var provider = new ServiceCollection()
            .AddSingleton<IToolExecutionAuditReader>(new InspectorProbeAuditReader())
            .AddSingleton<IBrowserSessionJournal>(journal)
            .AddSingleton<AuditPaneViewModel>()
            .BuildServiceProvider();
        using var pane = provider.GetRequiredService<AuditPaneViewModel>();
        pane.Attach(taskId);
        Assert.True(pane.HasBrowserSessions);
        Assert.Single(pane.BrowserSessions);
    }, TestContext.Current.CancellationToken);
}
