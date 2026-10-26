using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteSubagentRunJournalTests
{
    [Fact]
    public async Task BeginAsync_ReturnsFalseForMissingOrArchivedTasks()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        Assert.False(await journal.BeginAsync(Record(fixture, Guid.NewGuid()), fixture.Token));
        await fixture.ExecuteAsync(
            $"INSERT INTO archived_agent_tasks(task_id,archived_at) VALUES('{fixture.Task.Id}','2024-01-01');");
        Assert.False(await journal.BeginAsync(Record(fixture, fixture.Task.Id), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM subagent_runs;"));
    }

    [Fact]
    public async Task BeginAsync_RoundTripsEveryColumnInCreationOrder()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var first = Record(fixture, fixture.Task.Id,
            createdAt: new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero));
        var second = Record(fixture, fixture.Task.Id,
            createdAt: new DateTimeOffset(2024, 5, 1, 9, 0, 0, TimeSpan.Zero));
        Assert.True(await journal.BeginAsync(second, fixture.Token));
        Assert.True(await journal.BeginAsync(first, fixture.Token));

        var stored = await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token);

        Assert.Equal([first.RunId, second.RunId], stored.Select(item => item.RunId));
        var loaded = stored[0];
        Assert.Equal(first.TaskId, loaded.TaskId);
        Assert.Equal(first.ParentCallId, loaded.ParentCallId);
        Assert.Equal(first.ProfileName, loaded.ProfileName);
        Assert.Equal(first.SnapshotJson, loaded.SnapshotJson);
        Assert.Equal(SubagentRunStatus.Running, loaded.Status);
        Assert.Null(loaded.ErrorCode);
        Assert.Equal(first.CreatedAt, loaded.CreatedAt);
        Assert.Equal(first.UpdatedAt, loaded.UpdatedAt);
    }

    [Theory]
    [InlineData(SubagentRunStatus.Completed, null)]
    [InlineData(SubagentRunStatus.Failed, "subagent_model_failed")]
    [InlineData(SubagentRunStatus.Cancelled, null)]
    public async Task CompleteAsync_TransitionsRunningRowsToTerminalStates(
        SubagentRunStatus status, string? errorCode)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var record = Record(fixture, fixture.Task.Id);
        Assert.True(await journal.BeginAsync(record, fixture.Token));

        await journal.CompleteAsync(record.RunId, status, errorCode, fixture.Token);

        var stored = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(status, stored.Status);
        Assert.Equal(errorCode, stored.ErrorCode);
        Assert.True(stored.UpdatedAt >= record.UpdatedAt);
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotentOnceTerminalStatusExists()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var record = Record(fixture, fixture.Task.Id);
        Assert.True(await journal.BeginAsync(record, fixture.Token));
        await journal.CompleteAsync(record.RunId, SubagentRunStatus.Completed, null, fixture.Token);

        await journal.CompleteAsync(record.RunId, SubagentRunStatus.Failed, "late_failure", fixture.Token);
        await journal.CompleteAsync(record.RunId, SubagentRunStatus.Cancelled, null, fixture.Token);

        var stored = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(SubagentRunStatus.Completed, stored.Status);
        Assert.Null(stored.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_RejectsRunningStatusAndStoresBoundedErrorCodes()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var record = Record(fixture, fixture.Task.Id);
        Assert.True(await journal.BeginAsync(record, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            journal.CompleteAsync(record.RunId, SubagentRunStatus.Running, null, fixture.Token));
        var oversized = await Assert.ThrowsAsync<SqliteException>(() =>
            journal.CompleteAsync(record.RunId, SubagentRunStatus.Failed, new string('e', 129), fixture.Token));
        Assert.Equal(19, oversized.SqliteErrorCode);
        await journal.CompleteAsync(record.RunId, SubagentRunStatus.Failed, new string('e', 128), fixture.Token);
        var stored = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(new string('e', 128), stored.ErrorCode);
    }

    [Fact]
    public async Task MarkInterruptedRunningAsync_OnlyTouchesRunningRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var running = Record(fixture, fixture.Task.Id);
        var completed = Record(fixture, fixture.Task.Id);
        var failed = Record(fixture, fixture.Task.Id);
        foreach (var record in new[] { running, completed, failed })
            Assert.True(await journal.BeginAsync(record, fixture.Token));
        await journal.CompleteAsync(completed.RunId, SubagentRunStatus.Completed, null, fixture.Token);
        await journal.CompleteAsync(failed.RunId, SubagentRunStatus.Failed, "x", fixture.Token);

        Assert.Equal(1, await journal.MarkInterruptedRunningAsync(fixture.Token));
        Assert.Equal(0, await journal.MarkInterruptedRunningAsync(fixture.Token));

        var stored = await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(SubagentRunStatus.Interrupted,
            Assert.Single(stored, item => item.RunId == running.RunId).Status);
        Assert.Equal(SubagentRunStatus.Completed,
            Assert.Single(stored, item => item.RunId == completed.RunId).Status);
        Assert.Equal(SubagentRunStatus.Failed,
            Assert.Single(stored, item => item.RunId == failed.RunId).Status);
    }

    [Fact]
    public async Task ReadTaskAsync_IsBoundedAndOldestFirst()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        await fixture.ExecuteAsync($$"""
            WITH RECURSIVE seq(value) AS (SELECT 1 UNION ALL SELECT value+1 FROM seq WHERE value<300)
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            SELECT substr('00000000-0000-0000-0000-',1,24)||printf('%012d',value),'{{fixture.Task.Id}}','call-'||value,
                   'review','{}','running',NULL,
                   datetime('2024-01-01 00:00:00','+'||value||' seconds'),datetime('2024-01-01 00:00:00','+'||value||' seconds')
            FROM seq;
            """);

        var stored = await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token);

        Assert.Equal(256, stored.Count);
        var expectedOrder = stored
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.RunId.ToString("D"), StringComparer.Ordinal)
            .Select(item => item.RunId);
        Assert.Equal(expectedOrder, stored.Select(item => item.RunId));
        Assert.Equal(300, await fixture.CountAsync("SELECT COUNT(*) FROM subagent_runs;"));
    }

    [Fact]
    public async Task DeletingTheTaskCascadesItsRunRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var record = Record(fixture, fixture.Task.Id);
        Assert.True(await journal.BeginAsync(record, fixture.Token));
        await fixture.ExecuteAsync($"DELETE FROM agent_tasks WHERE id='{fixture.Task.Id}';");
        Assert.Empty(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM subagent_runs;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    private static SubagentRunRecord Record(SqliteFailurePersistenceFixture fixture, Guid taskId,
        DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), taskId, "call-1", "review", """{"profile":"review"}""",
            SubagentRunStatus.Running, null, now, now);
    }
}
