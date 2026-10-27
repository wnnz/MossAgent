using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteProxyFailureMigrationTests
{
    [Theory]
    [InlineData(TaskFailureReason.UnsupportedReasoningEffort)]
    [InlineData(TaskFailureReason.ProxyAuthentication)]
    public async Task VersionNineUpgrade_PreservesCatalogsAndHistory(TaskFailureReason reason)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var old = fixture.Task with { FailureReason = TaskFailureReason.ProviderHttp };
        await fixture.Repository.SaveTaskAsync(old, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(old.Id, true, fixture.Token);
        var archived = old with { Id = Guid.NewGuid(), Title = "archived failure" };
        await fixture.Repository.SaveTaskAsync(archived, fixture.Token);
        await fixture.Repository.SetTaskArchivedAsync(archived.Id, true, fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), old.Id, MessageRole.Assistant,
            "preserved partial reply", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await RestoreVersionNineAsync(fixture);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(old with { IsPinned = true }, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(archived, Assert.Single(await fixture.Repository.GetArchivedTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(old.Id, fixture.Token)));
        Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token));
        var created = old with { Id = Guid.NewGuid(), FailureReason = reason };
        await fixture.Repository.SaveTaskAsync(created, fixture.Token);
        Assert.Equal(created, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token), task => task.Id == created.Id));
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
        Assert.Equal(fixture.Project.AuthorizedDirectories, project.AuthorizedDirectories);
    }

    [Fact]
    public async Task FailedMigration_RollsBackConstraintAndHistoryBeforeRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var old = fixture.Task with { FailureReason = TaskFailureReason.ProviderAuthentication };
        await fixture.Repository.SaveTaskAsync(old, fixture.Token);
        await RestoreVersionNineAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_version_ten BEFORE INSERT ON schema_versions
            WHEN NEW.version=10 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=10;"));
        Assert.Equal(old, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_task_failures_v10';"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"UPDATE agent_task_failures SET reason=14 WHERE task_id='{old.Id}';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_version_ten;");
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(old, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
    }

    [Fact]
    public async Task CancelledInitialization_LeavesVersionNineUntouched()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersionNineAsync(fixture);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Initializer.InitializeAsync(cancellation.Token));

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=10;"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    private static Task RestoreVersionNineAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        CREATE TABLE agent_task_failures_v9 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12))
        );
        INSERT INTO agent_task_failures_v9 SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v9 RENAME TO agent_task_failures;
        DROP TABLE conversation_model_preferences;
        DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=10;
        """);
}
