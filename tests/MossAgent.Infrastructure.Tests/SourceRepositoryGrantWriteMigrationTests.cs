using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceRepositoryGrantWriteMigrationTests
{
    [Fact]
    public async Task FreshDatabaseAddsCanWriteColumnAtVersionFortyFive()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('source_repository_grants') WHERE name='can_write';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyFiveMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            ALTER TABLE source_repository_grants DROP COLUMN can_write;
            DELETE FROM schema_versions WHERE version=45;
            CREATE TRIGGER reject_version_forty_five BEFORE INSERT ON schema_versions
            WHEN NEW.version=45 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(44, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('source_repository_grants') WHERE name='can_write';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_five;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=45;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('source_repository_grants') WHERE name='can_write';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyFiveReplayAfterVersionRowLossSkipsExistingColumn()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", true);
        await grants.AddAsync(scope, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=45;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM source_repository_grants WHERE can_write=1;"));
        Assert.Equal(scope, Assert.Single(await new SqliteSourceRepositoryGrants(fixture.Connections)
            .GetAllAsync(fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyFiveUpgradeKeepsExistingGrantsReadOnlyWithoutBackfill()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(scope, fixture.Token);
        await fixture.ExecuteAsync("""
            ALTER TABLE source_repository_grants DROP COLUMN can_write;
            DELETE FROM schema_versions WHERE version=45;
            """);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM source_repository_grants WHERE can_write=0;"));
        Assert.Equal(scope, await grants.GetAsync(scope.GrantId, fixture.Token));
        Assert.False((await grants.GetAsync(scope.GrantId, fixture.Token))!.CanWrite);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
