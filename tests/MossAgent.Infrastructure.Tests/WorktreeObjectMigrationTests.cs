using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeObjectMigrationTests
{
    [Fact]
    public async Task Schema27UpgradePreservesLegacyHistoryAndRollsBackFailedEnrollmentMigration()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { WorktreePath = Path.Combine(fixture.Project.PrimaryDirectory, "legacy-worktree") };
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "retained legacy prompt", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(task, [message], fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE worktree_root_authorizations; DROP TABLE worktree_creation_intents; DROP TABLE worktree_cleanup_intents; DELETE FROM schema_versions WHERE version>=28; CREATE TRIGGER refuse_v28 BEFORE INSERT ON schema_versions WHEN NEW.version=28 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(27, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='worktree_root_authorizations';"));
        await fixture.ExecuteAsync("DROP TRIGGER refuse_v28;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM worktree_root_authorizations;"));
        Assert.Equal(task, Assert.Single(await fixture.Repository.GetTasksAsync(task.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
