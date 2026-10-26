using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ModelContextFailureMigrationTests
{
    [Fact]
    public async Task VersionTwentyMigrationPreservesHistoryAndAllowsContextFailuresAcrossReopen()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.AttachmentHistory };
        var message = new ConversationMessage(Guid.NewGuid(), failed.Id, MessageRole.User, "retained prompt", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(failed, [message], fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(failed.Id, true, fixture.Token);
        var archived = failed with { Id = Guid.NewGuid(), Title = "archived", FailureReason = TaskFailureReason.ProviderHttp };
        await fixture.Repository.SaveTaskAsync(archived, fixture.Token);
        await fixture.Repository.SetTaskArchivedAsync(archived.Id, true, fixture.Token);
        await RestoreVersionTwentyAsync(fixture);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(43, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var reopened = new SqliteWorkspaceRepository(fixture.Connections);
        Assert.Equal(failed with { IsPinned = true }, Assert.Single(await reopened.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal(archived, Assert.Single(await reopened.GetArchivedTasksAsync(archived.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await reopened.GetMessagesAsync(failed.Id, fixture.Token)));
        await reopened.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.ModelContextCapacity }, fixture.Token);
        var again = new SqliteWorkspaceRepository(fixture.Connections);
        Assert.Equal(TaskFailureReason.ModelContextCapacity, Assert.Single(await again.GetPinnedTasksAsync(fixture.Token)).FailureReason);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedMigrationRollsBackConstraintAndHistoryBeforeAnExplicitRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.AttachmentHistory };
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        await RestoreVersionTwentyAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_context_version BEFORE INSERT ON schema_versions
            WHEN NEW.version=21 BEGIN SELECT RAISE(ABORT, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(20, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(failed, Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_task_failures_v21';"));
        await fixture.ExecuteAsync("DELETE FROM agent_task_failures;");
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO agent_task_failures(task_id,reason) VALUES('{failed.Id}',16);"));
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        await fixture.ExecuteAsync("DROP TRIGGER reject_context_version;");

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Repository.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.ModelContextCapacity }, fixture.Token);

        Assert.Equal(43, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(16, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    private static Task RestoreVersionTwentyAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        CREATE TABLE agent_task_failures_v20 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15))
        );
        INSERT INTO agent_task_failures_v20(task_id,reason) SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v20 RENAME TO agent_task_failures;
        DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=21;
        """);
}
