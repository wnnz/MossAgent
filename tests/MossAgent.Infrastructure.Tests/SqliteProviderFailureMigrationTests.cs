using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteProviderFailureMigrationTests
{
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    public async Task VersionSevenUpgrade_PreservesHistoryAndAllowsNewFailureCategories(int category)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var old = fixture.Task with { FailureReason = TaskFailureReason.StreamLineLimit };
        await fixture.Repository.SaveTaskAsync(old, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(old.Id, true, fixture.Token);
        var archived = old with { Id = Guid.NewGuid(), Title = "archived failure", FailureReason = TaskFailureReason.ProviderAuthentication };
        await fixture.Repository.SaveTaskAsync(archived, fixture.Token);
        await fixture.Repository.SetTaskArchivedAsync(archived.Id, true, fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), old.Id, MessageRole.Assistant, "preserved partial reply", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await RestoreVersionSevenAsync(fixture);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(25, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(old with { IsPinned = true }, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(archived, Assert.Single(await fixture.Repository.GetArchivedTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(old.Id, fixture.Token)));
        Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token));
        var created = old with { Id = Guid.NewGuid(), FailureReason = (TaskFailureReason)category };
        await fixture.Repository.SaveTaskAsync(created, fixture.Token);
        Assert.Equal(created, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token), task => task.Id == created.Id));
        Assert.Equal(3, await fixture.CountAsync("SELECT COUNT(*) FROM agent_task_failures;"));
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
        Assert.Equal(fixture.Project.AuthorizedDirectories, project.AuthorizedDirectories);
    }

    [Fact]
    public async Task FailedMigration_RollsBackSchemaAndHistoryBeforeRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var old = fixture.Task with { FailureReason = TaskFailureReason.ProviderAuthentication };
        await fixture.Repository.SaveTaskAsync(old, fixture.Token);
        await RestoreVersionSevenAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_version_eight BEFORE INSERT ON schema_versions
            WHEN NEW.version=8 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=8;"));
        Assert.Equal(old, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_task_failures_v8';"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"UPDATE agent_task_failures SET reason=11 WHERE task_id='{old.Id}';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_version_eight;");
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(25, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(old, Assert.Single(await fixture.Repository.GetTasksAsync(old.ProjectId, fixture.Token)));
    }

    [Fact]
    public async Task CancelledInitialization_PreservesTheUnmigratedVersionSevenState()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersionSevenAsync(fixture);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Initializer.InitializeAsync(cancellation.Token));

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=8;"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    private static Task RestoreVersionSevenAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        CREATE TABLE agent_task_failures_v7 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10))
        );
        INSERT INTO agent_task_failures_v7 SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v7 RENAME TO agent_task_failures;
        DROP TABLE conversation_model_preferences;
        DELETE FROM schema_versions WHERE version>=8;
        """);
}
