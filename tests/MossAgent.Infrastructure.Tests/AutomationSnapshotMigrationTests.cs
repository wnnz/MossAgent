using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationSnapshotMigrationTests
{
    [Fact]
    public async Task FreshDatabase_CreatesSnapshotsTableAtVersionThirtyFour()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='automation_snapshots';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM automation_snapshots;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedForwardMigration_RollsBackAndPreservesSchedules()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteAutomationRepository(fixture.Connections);
        var plan = new AutomationSchedule(Guid.NewGuid(), "保留计划", "只读检查", fixture.Project.Id,
            Guid.NewGuid(), Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
            TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, true, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(await repository.SaveAsync(plan, fixture.Token));
        await fixture.ExecuteAsync("""
            DROP TABLE automation_snapshots;
            DELETE FROM schema_versions WHERE version>=34;
            CREATE TRIGGER reject_version_thirty_four BEFORE INSERT ON schema_versions
            WHEN NEW.version=34 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(33, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='automation_snapshots';"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM automation_schedules;"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirty_four;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var restored = Assert.Single(await repository.GetAllAsync(fixture.Token));
        Assert.Equal(plan.Id, restored.Id);
        Assert.Null(restored.AuthorizationSnapshot);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
