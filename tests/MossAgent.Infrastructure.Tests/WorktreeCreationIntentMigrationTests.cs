using Microsoft.Data.Sqlite;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeCreationIntentMigrationTests
{
    [Fact]
    public async Task FailedIntentMigrationRollsBackTableAndVersion()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE worktree_creation_intents;
            DELETE FROM schema_versions WHERE version=31;
            CREATE TRIGGER reject_v31 BEFORE INSERT ON schema_versions
            WHEN NEW.version=31 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(31, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='worktree_creation_intents';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_v31;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(32, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='worktree_creation_intents';"));
    }
}
