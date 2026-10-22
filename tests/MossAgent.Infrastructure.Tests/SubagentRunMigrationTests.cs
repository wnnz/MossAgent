using Microsoft.Data.Sqlite;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SubagentRunMigrationTests
{
    [Fact]
    public async Task FailedRunMigrationRollsBackTableAndVersion()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE subagent_runs;
            DELETE FROM schema_versions WHERE version=32;
            CREATE TRIGGER reject_v32 BEFORE INSERT ON schema_versions
            WHEN NEW.version=32 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(33, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='subagent_runs';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_v32;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(34, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='subagent_runs';"));
    }
}
