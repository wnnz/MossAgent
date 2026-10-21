using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationMigrationTests
{
    [Fact]
    public async Task Version25HistorySurvivesMigrationReopenAndNewFailurePersistence()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.ModelContextCapacity };
        var message = new ConversationMessage(Guid.NewGuid(), failed.Id, MessageRole.User, "retained", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(failed, [message], fixture.Token);
        await RestoreVersion25Async(fixture);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(28, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
        Assert.Equal(1, project.AuthorizationRevision);
        Assert.Equal(fixture.Project.Id, project.Id);
        Assert.Equal(failed, Assert.Single(await fixture.Repository.GetTasksAsync(project.Id, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(failed.Id, fixture.Token)));
        await fixture.Repository.SaveTaskAsync(failed with { FailureReason = TaskFailureReason.WorkspaceAuthorizationChanged }, fixture.Token);
        Assert.Equal(TaskFailureReason.WorkspaceAuthorizationChanged,
            Assert.Single(await fixture.Repository.GetTasksAsync(project.Id, fixture.Token)).FailureReason);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("UPDATE projects SET authorization_revision=0;"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("UPDATE projects SET authorization_revision=1.5;"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("UPDATE agent_task_failures SET reason=18;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedMigrationRollsBackColumnTriggerAndHistory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersion25Async(fixture);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_v26 BEFORE INSERT ON schema_versions WHEN NEW.version=26 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;");
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=26;"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("INSERT INTO schema_versions(version,applied_at) VALUES(26,'test');"));
        var migrationFailure = await Record.ExceptionAsync(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.True(migrationFailure is SqliteException or InvalidOperationException, $"Expected migration failure, got {migrationFailure?.GetType().Name ?? "none"}.");
        Assert.Equal(25, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('projects') WHERE name='authorization_revision';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('project_authorization_changed','agent_task_failures_v26');"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_v26;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(28, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    private static Task RestoreVersion25Async(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        DROP TRIGGER project_authorization_changed;
        ALTER TABLE projects DROP COLUMN authorization_revision;
        CREATE TABLE agent_task_failures_v25 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16))
        );
        INSERT INTO agent_task_failures_v25 SELECT * FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v25 RENAME TO agent_task_failures;
        DROP TABLE project_root_authorizations;
        DELETE FROM schema_versions WHERE version>=26;
        """);
}
