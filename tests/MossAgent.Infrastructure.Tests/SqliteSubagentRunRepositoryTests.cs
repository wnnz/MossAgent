using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteSubagentRunRepositoryTests
{
    [Fact]
    public async Task StartTerminalStatesAndTaskOwnedReadsPersist()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var run = CreateRun(fixture.Task.Id);

        var started = await repository.StartAsync(run, fixture.Token);
        var completed = await repository.CompleteAsync(fixture.Task.Id, run.Id, "finished", fixture.Token);

        Assert.Equal(SubagentRunStatus.Running, started.Status);
        Assert.Equal(SubagentRunStatus.Completed, completed.Status);
        Assert.Equal("finished", completed.Summary);
        Assert.NotNull(completed.CompletedAt);
        var restored = Assert.Single(await repository.GetRecentAsync(fixture.Task.Id, 10, fixture.Token));
        Assert.Equal(SubagentRunStatus.Completed, restored.Status);
        Assert.Equal(run.AgentName, restored.AgentName);
        Assert.Equal(run.ModelId, restored.ModelId);
        Assert.Empty(await repository.GetRecentAsync(Guid.NewGuid(), 10, fixture.Token));
    }

    [Fact]
    public async Task FailedAndCancelledRunsKeepOnlyBoundedSafeTerminalMetadata()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var failed = CreateRun(fixture.Task.Id);
        var cancelled = CreateRun(fixture.Task.Id);
        await repository.StartAsync(failed, fixture.Token);
        await repository.StartAsync(cancelled, fixture.Token);

        var failedResult = await repository.FailAsync(fixture.Task.Id, failed.Id,
            new string('x', 4095) + "😀tail", "subagent_model_failed", fixture.Token);
        var cancelledResult = await repository.CancelAsync(fixture.Task.Id, cancelled.Id,
            "cancelled", fixture.Token);

        Assert.Equal(SubagentRunStatus.Failed, failedResult.Status);
        Assert.Equal(4095, failedResult.Summary!.Length);
        Assert.DoesNotContain(failedResult.Summary, char.IsSurrogate);
        Assert.Equal("subagent_model_failed", failedResult.ErrorCode);
        Assert.Equal(SubagentRunStatus.Cancelled, cancelledResult.Status);
        Assert.Equal("tool_cancelled", cancelledResult.ErrorCode);
    }

    [Fact]
    public async Task AnotherInstanceStartupDoesNotRewriteAnOwnedRunningRun()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var owner = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var observer = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var run = CreateRun(fixture.Task.Id);
        await owner.StartAsync(run, fixture.Token);

        await fixture.Initializer.InitializeAsync(fixture.Token);

        var restored = Assert.Single(await observer.GetRecentAsync(fixture.Task.Id, 10, fixture.Token));
        Assert.Equal(SubagentRunStatus.Running, restored.Status);
        Assert.Equal(owner.InstanceId, restored.OwnerInstanceId);
        Assert.NotEqual(observer.InstanceId, restored.OwnerInstanceId);
        Assert.Null(restored.CompletedAt);
    }

    [Fact]
    public async Task OnlyAnotherInstanceCanAcknowledgeAnAbandonedRunAsInterrupted()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var owner = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var observer = new SqliteSubagentRunRepository(fixture.Connections, TimeProvider.System);
        var run = CreateRun(fixture.Task.Id);
        await owner.StartAsync(run, fixture.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.MarkInterruptedAsync(fixture.Task.Id, run.Id, "wrong owner", fixture.Token));
        var interrupted = await observer.MarkInterruptedAsync(fixture.Task.Id, run.Id,
            "旧实例已停止，副作用已人工核实。", fixture.Token);

        Assert.Equal(SubagentRunStatus.Interrupted, interrupted.Status);
        Assert.Equal(owner.InstanceId, interrupted.OwnerInstanceId);
        Assert.Equal("subagent_run_interrupted", interrupted.ErrorCode);
        Assert.Contains("人工核实", interrupted.Summary);
    }

    private static SubagentRun CreateRun(Guid taskId)
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), taskId, Guid.NewGuid(), "parent-call",
            Guid.NewGuid(), "reviewer", "需要审查时", "Review the change.",
            Guid.NewGuid(), "provider", ProviderProtocol.OpenAiResponses,
            Guid.NewGuid(), "model", 128000, 4096, "high", true,
            Guid.NewGuid().ToString("N"), SubagentRunStatus.Running, null, null, now, now, null);
    }
}
