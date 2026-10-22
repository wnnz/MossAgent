using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditPaneSubagentRunTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task RuntimeUpdatesAndInterruptedRunsAppearInTheCurrentTaskOrder() =>
        _session.DispatchAsync(async () =>
        {
            var taskId = Guid.NewGuid();
            var staleRun = Run(taskId, SubagentRunStatus.Running,
                DateTimeOffset.UtcNow.AddSeconds(2), "old-instance");
            var interruptedAt = DateTimeOffset.UtcNow.AddSeconds(4);
            var interruptCalls = 0;
            var runs = new InspectorProbeSubagentRunRepository
            {
                InstanceId = "current-instance",
                Recent = (_, _, _) => Task.FromResult<IReadOnlyList<SubagentRun>>(
                    [
                        Run(Guid.NewGuid(), SubagentRunStatus.Completed, DateTimeOffset.UtcNow.AddSeconds(3)),
                        staleRun,
                        Run(taskId, SubagentRunStatus.Interrupted, DateTimeOffset.UtcNow.AddSeconds(1)),
                        Run(taskId, SubagentRunStatus.Running, DateTimeOffset.UtcNow, "current-instance")
                    ]),
                Interrupt = (_, runId, summary, _) =>
                {
                    interruptCalls++;
                    return Task.FromResult(staleRun with
                    {
                        Id = runId,
                        Status = SubagentRunStatus.Interrupted,
                        Summary = summary,
                        ErrorCode = "subagent_run_interrupted",
                        UpdatedAt = interruptedAt,
                        CompletedAt = interruptedAt
                    });
                }
            };
            using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), runs);

            pane.Attach(taskId);
            await WaitUntilAsync(() => !pane.IsLoading, Token);

            Assert.True(pane.HasSubagentRuns);
            Assert.Equal(3, pane.SubagentRuns.Count);
            Assert.DoesNotContain(pane.SubagentRuns, item => item.TaskId != taskId);
            Assert.Contains(pane.SubagentRuns, item => item.Status == SubagentRunStatus.Interrupted);
            var stale = Assert.Single(pane.SubagentRuns, item => item.NeedsRecovery);
            Assert.Equal("需要核实", stale.StatusLabel);
            Assert.Contains(pane.SubagentRuns,
                item => item.AgentName == "review" && item.StatusLabel == "运行中");
            Assert.Equal("最近 3 次子代理运行", pane.SubagentRunsStatus);
            Assert.True(stale.CanAcknowledge);
            await pane.AcknowledgeSubagentRunCommand.ExecuteAsync(stale);
            Assert.True(stale.IsConfirmationPending);
            Assert.Equal("确认标记为已中断", stale.AcknowledgeLabel);
            Assert.Equal(0, interruptCalls);
            await pane.AcknowledgeSubagentRunCommand.ExecuteAsync(stale);
            Assert.Equal(1, interruptCalls);
            var acknowledged = Assert.Single(pane.SubagentRuns, item => item.Id == stale.Id);
            Assert.False(acknowledged.NeedsRecovery);
            Assert.False(acknowledged.CanAcknowledge);
            Assert.Equal(SubagentRunStatus.Interrupted, acknowledged.Status);
            Assert.Equal("已中断", acknowledged.StatusLabel);
        }, Token);

    [Fact]
    public Task SubagentReadFailureDoesNotHideToolAuditFailureOrLeakDetails() =>
        _session.DispatchAsync(async () =>
        {
            var taskId = Guid.NewGuid();
            var runs = new InspectorProbeSubagentRunRepository
            {
                Recent = (_, _, _) => throw new IOException("synthetic secret")
            };
            using var pane = new AuditPaneViewModel(new InspectorProbeAuditReader(), runs);

            pane.Attach(taskId);
            await WaitUntilAsync(() => !pane.IsLoading, Token);

            Assert.False(pane.HasSubagentRuns);
            Assert.Equal(MossAgent.App.Resources.InspectorText.Get("SubagentRunsFailed"),
                pane.SubagentRunsStatus);
            Assert.DoesNotContain("synthetic secret", pane.SubagentRunsStatus);
        }, Token);

    [Fact]
    public Task ToolAuditFailureDoesNotDiscardAlreadyLoadedSubagentRuns() =>
        _session.DispatchAsync(async () =>
        {
            var taskId = Guid.NewGuid();
            var reader = new InspectorProbeAuditReader
            {
                Recent = (_, _) => throw new IOException("synthetic audit failure")
            };
            var runs = new InspectorProbeSubagentRunRepository
            {
                Recent = (_, _, _) => Task.FromResult<IReadOnlyList<SubagentRun>>(
                    [Run(taskId, SubagentRunStatus.Completed, DateTimeOffset.UtcNow)])
            };
            using var pane = new AuditPaneViewModel(reader, runs);

            pane.Attach(taskId);
            await WaitUntilAsync(() => !pane.IsLoading, Token);

            Assert.Single(pane.SubagentRuns);
            Assert.True(pane.HasSubagentRuns);
            Assert.Equal(MossAgent.App.Resources.InspectorText.Get("AuditFailed"), pane.Status);
            Assert.DoesNotContain("synthetic audit failure", pane.Status);
        }, Token);

    private static SubagentRun Run(Guid taskId, SubagentRunStatus status, DateTimeOffset createdAt,
        string? ownerInstanceId = null) =>
        new(Guid.NewGuid(), taskId, Guid.NewGuid(), "call", Guid.NewGuid(), "review",
            "trigger", "prompt", Guid.NewGuid(), "provider", ProviderProtocol.OpenAiResponses,
            Guid.NewGuid(), "model", 128000, 4096, "high", true,
            ownerInstanceId ?? Guid.NewGuid().ToString("N"), status,
            status == SubagentRunStatus.Interrupted ? "应用上次关闭时仍在运行。" : null,
            status == SubagentRunStatus.Interrupted ? "subagent_run_interrupted" : null,
            createdAt, createdAt, status == SubagentRunStatus.Running ? null : createdAt);

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(15, token);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
