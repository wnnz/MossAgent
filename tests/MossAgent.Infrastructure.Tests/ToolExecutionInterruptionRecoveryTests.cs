using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ToolExecutionInterruptionRecoveryTests
{
    [Theory]
    [InlineData(AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.WaitingForApproval)]
    [InlineData(AgentTaskStatus.Completed)]
    public async Task StartupMarksOnlyUnfinishedAuditsWithoutInventingResults(AgentTaskStatus status)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Status = status }, fixture.Token);
        var sink = new SqliteToolExecutionAuditSink(fixture.Connections);
        var pendingId = await StartAsync(fixture, sink, "pending");
        var completedId = await StartAsync(fixture, sink, "completed");
        var completion = new ToolExecutionAuditCompletion(DateTimeOffset.UtcNow, true, true, null, 42);
        await sink.CompleteAsync(completedId, completion, [new("result.txt", "artifact-only", "text/plain", 42, "HASH")], fixture.Token);
        var reader = new SqliteToolExecutionAuditReader(fixture.Connections);
        var before = await reader.GetRecentAsync(fixture.Task.Id, 10, fixture.Token);
        Assert.All(before, item => Assert.Null(item.InterruptedAt));
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var recovered = await reader.GetRecentAsync(fixture.Task.Id, 10, fixture.Token);
        var pending = Assert.Single(recovered, item => item.Id == pendingId);
        Assert.NotNull(pending.InterruptedAt);
        Assert.Null(pending.CompletedAt);
        Assert.Null(pending.IsSuccess);
        Assert.Null(pending.ApprovalGranted);
        Assert.Null(pending.ErrorCode);
        Assert.Null(pending.OutputLength);
        Assert.Equal(Assert.Single(before, item => item.Id == completedId), Assert.Single(recovered, item => item.Id == completedId));
        Assert.Single(await reader.GetArtifactsAsync(completedId, fixture.Token));
        var expectedStatus = status == AgentTaskStatus.Completed ? status : AgentTaskStatus.Interrupted;
        Assert.Equal(expectedStatus, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)).Status);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(pending, Assert.Single(await reader.GetRecentAsync(fixture.Task.Id, 10, fixture.Token), item => item.Id == pendingId));
        var freshId = await StartAsync(fixture, sink, "fresh-live-run");
        Assert.Null(Assert.Single(await reader.GetRecentAsync(fixture.Task.Id, 10, fixture.Token), item => item.Id == freshId).InterruptedAt);
        await sink.CompleteAsync(pendingId, completion, [], fixture.Token);
        var actualCompletion = Assert.Single(await reader.GetRecentAsync(fixture.Task.Id, 10, fixture.Token), item => item.Id == pendingId);
        Assert.Equal(completion.CompletedAt, actualCompletion.CompletedAt);
        Assert.True(actualCompletion.IsSuccess);
        Assert.Equal(pending.InterruptedAt, actualCompletion.InterruptedAt);
    }

    [Fact]
    public async Task RecoveryFailureRollsBackTaskStatusAndAllInterruptionMarkers()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Status = AgentTaskStatus.Running }, fixture.Token);
        var sink = new SqliteToolExecutionAuditSink(fixture.Connections);
        await StartAsync(fixture, sink, "uncertain");
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_interruption BEFORE INSERT ON tool_execution_interruptions
            BEGIN SELECT RAISE(ABORT,'synthetic recovery failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)).Status);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM tool_execution_interruptions;"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_interruption;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM tool_execution_interruptions;"));
    }

    [Fact]
    public async Task CancelledInitializationDoesNotChangeActiveTaskOrAudit()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Status = AgentTaskStatus.Running }, fixture.Token);
        await StartAsync(fixture, new(fixture.Connections), "uncertain");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Initializer.InitializeAsync(cancellation.Token));
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)).Status);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM tool_execution_interruptions;"));
    }

    private static Task<Guid> StartAsync(SqliteFailurePersistenceFixture fixture, SqliteToolExecutionAuditSink sink, string call) =>
        sink.StartAsync(new(fixture.Task.Id, call, "browser.navigate", ToolRiskLevel.ExternalSideEffect,
            ApprovalPolicy.AskEveryTime, DateTimeOffset.UtcNow), fixture.Token);
}
