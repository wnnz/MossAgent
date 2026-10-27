using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteInstalledPluginStoreTests
{
    private static readonly PluginComponentDescriptor[] Components =
        [new(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")];

    private static readonly PluginMcpServerDeclaration[] Servers =
        [new("alpha", "stdio")];

    private static InstalledPlugin Row(string name, string version, string hash, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, version, hash, $"{name}-{version}-{hash[..8]}",
            InstalledPluginState.Installed, "/tmp/pkg.mossplugin", DateTimeOffset.UnixEpoch,
            Components, Servers, PluginOverlaySource.ExtensionsObject);

    [Fact]
    public async Task InsertOfIdenticalContentIsIdempotentAndReturnsTheStoredRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)), fixture.Token);
        var duplicate = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)), fixture.Token);
        Assert.Equal(first.Id, duplicate.Id);
        PluginRowAssert.Equal(first, duplicate);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task InsertWithADifferentHashUnderTheSameKeyConflicts()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        await store.InsertAsync(Row("demo", "1.0", new string('a', 64)), fixture.Token);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            store.InsertAsync(Row("demo", "1.0", new string('b', 64)), fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallConflict, error.Code);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task GetAndListRoundTripRowsInNameOrder()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var beta = await store.InsertAsync(Row("beta", "2.0", new string('c', 64)), fixture.Token);
        var alpha = await store.InsertAsync(Row("alpha", "1.0", new string('d', 64)), fixture.Token);
        Assert.Null(await store.GetAsync(Guid.NewGuid(), fixture.Token));
        PluginRowAssert.Equal(alpha, await store.GetAsync(alpha.Id, fixture.Token));
        PluginRowAssert.Equal(beta, await store.GetByNameVersionAsync("beta", "2.0", fixture.Token));
        Assert.Null(await store.GetByNameVersionAsync("beta", "9.9", fixture.Token));
        var rows = await store.ListAsync(fixture.Token);
        Assert.Equal(2, rows.Count);
        PluginRowAssert.Equal(alpha, rows[0]);
        PluginRowAssert.Equal(beta, rows[1]);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task OversizedComponentMetadataIsRejectedBeforeAnyWrite()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var oversized = new InstalledPlugin(Guid.NewGuid(), "demo", "1.0", new string('a', 64),
            "demo-1-0-aaaaaaaa", InstalledPluginState.Installed, "/tmp/p.mossplugin",
            DateTimeOffset.UnixEpoch,
            [new PluginComponentDescriptor(PluginComponentKind.Asset, new string('n', 9000), "n")],
            [], PluginOverlaySource.None);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Store().InsertAsync(oversized, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task SetStateRoundTripsThroughEveryLifecycleValue()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)), fixture.Token);
        Assert.Equal(InstalledPluginState.Installed, row.State);
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Enabled },
            await store.SetStateAsync(row.Id, InstalledPluginState.Enabled, fixture.Token));
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Disabled },
            await store.SetStateAsync(row.Id, InstalledPluginState.Disabled, fixture.Token));
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Enabled },
            await store.SetStateAsync(row.Id, InstalledPluginState.Enabled, fixture.Token));
        Assert.Equal(InstalledPluginState.Enabled, (await store.GetAsync(row.Id, fixture.Token))!.State);
    }

    [Fact]
    public async Task SetStateOnAMissingRowReturnsNull()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        Assert.Null(await fixture.Store().SetStateAsync(Guid.NewGuid(),
            InstalledPluginState.Enabled, fixture.Token));
    }

    [Fact]
    public async Task DeleteReturnsTheStoredRowAndRemovesIt()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)), fixture.Token);
        PluginRowAssert.Equal(row, await store.DeleteAsync(row.Id, fixture.Token));
        Assert.Null(await store.GetAsync(row.Id, fixture.Token));
        Assert.Null(await store.DeleteAsync(row.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task SchemaViolationWithoutARowIsAFailureNotAConflict()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var badSource = new InstalledPlugin(Guid.NewGuid(), "demo", "1.0", new string('a', 64),
            "demo-1-0-aaaaaaaa", InstalledPluginState.Installed, new string('s', 1100),
            DateTimeOffset.UnixEpoch, Components, Servers, PluginOverlaySource.None);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Store().InsertAsync(badSource, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }
}
