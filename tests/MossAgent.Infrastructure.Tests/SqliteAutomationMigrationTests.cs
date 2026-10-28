using Microsoft.Data.Sqlite;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteAutomationMigrationTests
{
    [Fact]
    public async Task FailedForwardMigration_RollsBackNewTablesAndPreservesExistingHistory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE automation_snapshots;
            DROP TABLE automation_runs;
            DROP TABLE automation_schedules;
            DROP TABLE conversation_model_preferences;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=9;
            CREATE TRIGGER reject_version_nine BEFORE INSERT ON schema_versions
            WHEN NEW.version=9 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(8, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'automation_%';"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));

        await fixture.ExecuteAsync("DROP TRIGGER reject_version_nine;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(3, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name LIKE 'automation_%';"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }
}
