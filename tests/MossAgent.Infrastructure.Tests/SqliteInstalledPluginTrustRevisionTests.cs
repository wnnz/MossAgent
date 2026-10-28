using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteInstalledPluginTrustRevisionTests
{
    private static readonly PluginComponentDescriptor[] Components =
        [new(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")];

    private static InstalledPlugin Row(string name, string version, string hash) =>
        new(Guid.NewGuid(), name, version, hash, $"{name}-{version}-{hash[..8]}",
            InstalledPluginState.Installed, "/tmp/pkg.mossplugin", DateTimeOffset.UnixEpoch,
            Components, [], PluginOverlaySource.None, 9);

    [Fact]
    public async Task InsertAndReplaceAlwaysStartTheCounterAtOne()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        // The record carries 9 to prove the store owns the counter, not the caller.
        var inserted = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(1, inserted.TrustRevision);
        Assert.Equal(1, (await store.GetAsync(inserted.Id, fixture.Token))!.TrustRevision);
        var replaced = await store.ReplaceByNameAsync(Row("other", "2.0", new string('b', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(1, replaced.TrustRevision);
        Assert.Equal(2, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_packages WHERE trust_revision=1;"));
    }

    [Fact]
    public async Task SetStateBumpsTheRevisionOnEveryRealTransition()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(1, row.TrustRevision);

        Assert.Equal(2, (await store.SetStateAsync(row.Id, InstalledPluginState.Enabled,
            fixture.Token))!.TrustRevision);
        Assert.Equal(3, (await store.SetStateAsync(row.Id, InstalledPluginState.Disabled,
            fixture.Token))!.TrustRevision);
        Assert.Equal(4, (await store.SetStateAsync(row.Id, InstalledPluginState.Drifted,
            fixture.Token))!.TrustRevision);
        Assert.Equal(4, (await store.GetAsync(row.Id, fixture.Token))!.TrustRevision);
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_packages WHERE trust_revision=4 AND state=3;"));
    }

    [Fact]
    public async Task SameStateCallsReturnTheRowWithoutBumping()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(1, (await store.SetStateAsync(row.Id, InstalledPluginState.Installed,
            fixture.Token))!.TrustRevision);
        var enabled = await store.SetStateAsync(row.Id, InstalledPluginState.Enabled, fixture.Token);
        Assert.Equal(2, enabled!.TrustRevision);
        // A same-state call is a no-op that still returns the row — drift marking
        // Drifted-to-Drifted must not mint a new generation either.
        PluginRowAssert.Equal(enabled,
            await store.SetStateAsync(row.Id, InstalledPluginState.Enabled, fixture.Token));
        var drifted = await store.SetStateAsync(row.Id, InstalledPluginState.Drifted, fixture.Token);
        Assert.Equal(3, drifted!.TrustRevision);
        PluginRowAssert.Equal(drifted,
            await store.SetStateAsync(row.Id, InstalledPluginState.Drifted, fixture.Token));
        Assert.Equal(3, (await store.GetAsync(row.Id, fixture.Token))!.TrustRevision);
    }

    [Fact]
    public async Task ReplaceByNameRestartsAtOneAfterTheOldRowClimbed()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var old = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        await store.SetStateAsync(old.Id, InstalledPluginState.Enabled, fixture.Token);
        await store.SetStateAsync(old.Id, InstalledPluginState.Disabled, fixture.Token);
        Assert.Equal(3, (await store.GetAsync(old.Id, fixture.Token))!.TrustRevision);

        var replaced = await store.ReplaceByNameAsync(Row("demo", "2.0", new string('b', 64)),
            new Dictionary<string, string>(), fixture.Token);

        Assert.NotEqual(old.Id, replaced.Id);
        Assert.Equal(1, replaced.TrustRevision);
        Assert.Equal(1, (await store.GetAsync(replaced.Id, fixture.Token))!.TrustRevision);
        Assert.Null(await store.GetAsync(old.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_packages WHERE trust_revision=1;"));
    }
}
