using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationRunGenerationMigrationTests
{
    [Fact]
    public async Task FreshDatabaseAddsRunGenerationColumnAtVersionFortyTwo()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('automation_runs') WHERE name='snapshot_generation';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyTwoMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            ALTER TABLE automation_runs DROP COLUMN snapshot_generation;
            DELETE FROM schema_versions WHERE version=42;
            CREATE TRIGGER reject_version_forty_two BEFORE INSERT ON schema_versions
            WHEN NEW.version=42 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('automation_runs') WHERE name='snapshot_generation';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_two;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=42;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('automation_runs') WHERE name='snapshot_generation';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyTwoUpgradeKeepsExistingRunsGenerationUnknownWithoutBackfill()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteAutomationRepository(fixture.Connections);
        var plan = new AutomationSchedule(Guid.NewGuid(), "迁移计划", "只读检查", fixture.Project.Id,
            Guid.NewGuid(), Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
            TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, true, DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.True(await repository.SaveAsync(plan, fixture.Token));
        var finished = DateTimeOffset.UtcNow.ToUniversalTime().ToString("O");
        await fixture.ExecuteAsync($"""
            INSERT INTO automation_runs(id,schedule_id,task_id,started_at,finished_at,status,manual)
            VALUES('{Guid.NewGuid()}','{plan.Id}','{Guid.NewGuid()}','{finished}','{finished}',1,0);
            ALTER TABLE automation_runs DROP COLUMN snapshot_generation;
            DELETE FROM schema_versions WHERE version=42;
            """);

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var legacy = Assert.Single(await repository.GetRunsAsync(plan.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Completed, legacy.Status);
        Assert.Null(legacy.SnapshotGeneration);
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM automation_runs WHERE snapshot_generation IS NULL;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
