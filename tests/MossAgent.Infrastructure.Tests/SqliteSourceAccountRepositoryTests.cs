using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteSourceAccountRepositoryTests
{
    [Fact]
    public async Task VersionSeventeenMigrationRollsBackThenReopensWithoutLosingUserData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("DROP TABLE source_accounts; DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=18;");
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_eighteen BEFORE INSERT ON schema_versions
            WHEN NEW.version=18 BEGIN SELECT RAISE(FAIL,'fixture migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(17, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='source_accounts';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_eighteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var repository = new SqliteSourceAccountRepository(fixture.Connections);
        var saved = await repository.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var reopened = new SqliteSourceAccountRepository(fixture.Connections);
        var restored = Assert.Single(await reopened.GetAllAsync(fixture.Token));
        Assert.Equal(saved.Id, restored.Id);
        Assert.Equal(saved.AccessToken, restored.AccessToken);
        Assert.Equal(saved.ApiEndpoint, restored.ApiEndpoint);
        Assert.Equal(1, restored.Revision);
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Project.Id, Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token)).Id);
        Assert.Equal(fixture.Task.Id, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Id);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task StaleUpdatesAndDeletesCannotOverwriteOrResurrectAccount()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteSourceAccountRepository(fixture.Connections);
        var draft = SourceAccountValidationTests.Create();
        var saved = await repository.SaveAsync(draft, fixture.Token);
        var updated = await repository.SaveAsync(saved, fixture.Token);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(SourceConnectionFailure.AccountChanged,
            (await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(saved, fixture.Token))).Failure);
        await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(draft, fixture.Token));
        await Assert.ThrowsAsync<SourceConnectionException>(() => repository.DeleteAsync(saved.Id, saved.Revision, fixture.Token));
        await repository.DeleteAsync(updated.Id, updated.Revision, fixture.Token);
        await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(updated, fixture.Token));
        Assert.Null(await repository.GetAsync(saved.Id, fixture.Token));
    }

    [Fact]
    public async Task ReferencedProxyCannotBeDeletedAndDisabledProxyCannotBeSaved()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        var proxy = new ProxyProfile(Guid.NewGuid(), "fixture", ProxyProtocol.Http, "127.0.0.1", 23456,
            null, null, false, true);
        await configurations.SaveProxyAsync(proxy, fixture.Token);
        var repository = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await repository.SaveAsync(SourceAccountValidationTests.Create(proxyId: proxy.Id), fixture.Token);
        await Assert.ThrowsAsync<ProxyInUseException>(() => configurations.DeleteProxyAsync(proxy.Id, fixture.Token));
        await configurations.SaveProxyAsync(proxy with { IsEnabled = false }, fixture.Token);
        Assert.Equal(SourceConnectionFailure.ProxyUnavailable,
            (await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(account, fixture.Token))).Failure);
        Assert.Equal(account.Revision, (await repository.GetAsync(account.Id, fixture.Token))!.Revision);
        await repository.DeleteAsync(account.Id, account.Revision, fixture.Token);
        await configurations.DeleteProxyAsync(proxy.Id, fixture.Token);
        Assert.Empty(await configurations.GetProxiesAsync(fixture.Token));
    }

    [Fact]
    public async Task DisablingAccountPreservesUnavailableProxyAndStillBlocksReenable()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        var proxy = new ProxyProfile(Guid.NewGuid(), "fixture", ProxyProtocol.Http, "127.0.0.1", 23456,
            null, null, false, true);
        await configurations.SaveProxyAsync(proxy, fixture.Token);
        var repository = new SqliteSourceAccountRepository(fixture.Connections);
        var saved = await repository.SaveAsync(SourceAccountValidationTests.Create(proxyId: proxy.Id), fixture.Token);
        await configurations.SaveProxyAsync(proxy with { IsEnabled = false }, fixture.Token);
        var disabled = await repository.SaveAsync(new(saved.Id, saved.Name, saved.ApiEndpoint, saved.AccessToken,
            saved.ProxyId, false, saved.Revision), fixture.Token);
        Assert.False(disabled.IsEnabled);
        Assert.Equal(proxy.Id, disabled.ProxyId);
        var failure = await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(
            new(disabled.Id, disabled.Name, disabled.ApiEndpoint, disabled.AccessToken, disabled.ProxyId,
                true, disabled.Revision), fixture.Token));
        Assert.Equal(SourceConnectionFailure.ProxyUnavailable, failure.Failure);
        var stored = await repository.GetAsync(saved.Id, fixture.Token);
        Assert.False(stored!.IsEnabled);
        Assert.Equal(proxy.Id, stored.ProxyId);
        Assert.Equal(disabled.Revision, stored.Revision);
    }

    [Fact]
    public async Task DisabledAccountStillCannotReferenceMissingProxy()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteSourceAccountRepository(fixture.Connections);
        var failure = await Assert.ThrowsAsync<SourceConnectionException>(() => repository.SaveAsync(new(
            Guid.NewGuid(), "disabled", new Uri("https://api.github.com/"), "synthetic-missing-proxy-credential",
            Guid.NewGuid(), false), fixture.Token));
        Assert.Equal(SourceConnectionFailure.ProxyUnavailable, failure.Failure);
        Assert.Empty(await repository.GetAllAsync(fixture.Token));
    }
}
