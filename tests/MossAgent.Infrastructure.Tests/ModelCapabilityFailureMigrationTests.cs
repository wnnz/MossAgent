using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ModelCapabilityFailureMigrationTests
{
    [Fact]
    public async Task VersionThirtyFourHistorySurvivesMigrationAndAllowsCapabilityFailures()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.WorkspaceAuthorizationChanged };
        var message = new ConversationMessage(Guid.NewGuid(), failed.Id, MessageRole.User, "retained prompt", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(failed, [message], fixture.Token);
        await RestoreVersionThirtyFourAsync(fixture);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_task_failures_v35';"));
        var reopened = new SqliteWorkspaceRepository(fixture.Connections);
        Assert.Equal(failed, Assert.Single(await reopened.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await reopened.GetMessagesAsync(failed.Id, fixture.Token)));
        await reopened.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.UnsupportedImageInput }, fixture.Token);
        Assert.Equal(TaskFailureReason.UnsupportedImageInput,
            Assert.Single(await reopened.GetTasksAsync(failed.ProjectId, fixture.Token)).FailureReason);
        await reopened.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.InvalidModelParameters }, fixture.Token);
        Assert.Equal(TaskFailureReason.InvalidModelParameters,
            Assert.Single(await reopened.GetTasksAsync(failed.ProjectId, fixture.Token)).FailureReason);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("UPDATE agent_task_failures SET reason=20;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedMigrationRollsBackConstraintBeforeAnExplicitRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.WorkspaceAuthorizationChanged };
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        await RestoreVersionThirtyFourAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_capability_version BEFORE INSERT ON schema_versions
            WHEN NEW.version=35 BEGIN SELECT RAISE(ABORT, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(34, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(failed, Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_task_failures_v35';"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"UPDATE agent_task_failures SET reason=18 WHERE task_id='{failed.Id}';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_capability_version;");

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        await fixture.Repository.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.UnsupportedImageInput }, fixture.Token);
        Assert.Equal(18, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    private static Task RestoreVersionThirtyFourAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        CREATE TABLE agent_task_failures_v34 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17))
        );
        INSERT INTO agent_task_failures_v34(task_id,reason) SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v34 RENAME TO agent_task_failures;
        DELETE FROM schema_versions WHERE version>=35;
        """);
}
