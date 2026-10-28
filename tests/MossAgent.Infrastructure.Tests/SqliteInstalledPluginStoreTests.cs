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
            Components, Servers, PluginOverlaySource.ExtensionsObject, 1);

    [Fact]
    public async Task InsertOfIdenticalContentIsIdempotentAndReturnsTheStoredRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        var duplicate = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(first.Id, duplicate.Id);
        PluginRowAssert.Equal(first, duplicate);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task InsertWithADifferentHashUnderTheSameKeyConflicts()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            store.InsertAsync(Row("demo", "1.0", new string('b', 64)),
                new Dictionary<string, string>(), fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallConflict, error.Code);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task GetAndListRoundTripRowsInNameOrder()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var beta = await store.InsertAsync(Row("beta", "2.0", new string('c', 64)),
            new Dictionary<string, string>(), fixture.Token);
        var alpha = await store.InsertAsync(Row("alpha", "1.0", new string('d', 64)),
            new Dictionary<string, string>(), fixture.Token);
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
            [], PluginOverlaySource.None, 1);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Store().InsertAsync(oversized, new Dictionary<string, string>(), fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task SetStateRoundTripsThroughEveryLifecycleValue()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(InstalledPluginState.Installed, row.State);
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Enabled, TrustRevision = 2 },
            await store.SetStateAsync(row.Id, InstalledPluginState.Enabled, fixture.Token));
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Disabled, TrustRevision = 3 },
            await store.SetStateAsync(row.Id, InstalledPluginState.Disabled, fixture.Token));
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Enabled, TrustRevision = 4 },
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
    public async Task ListByNameReturnsEveryVersionOfOneNameOnly()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        var second = await store.InsertAsync(Row("demo", "2.0", new string('b', 64)),
            new Dictionary<string, string>(), fixture.Token);
        await store.InsertAsync(Row("other", "1.0", new string('c', 64)),
            new Dictionary<string, string>(), fixture.Token);

        var rows = await store.ListByNameAsync("demo", fixture.Token);

        Assert.Equal(2, rows.Count);
        PluginRowAssert.Equal(first, rows[0]);
        PluginRowAssert.Equal(second, rows[1]);
        Assert.Empty(await store.ListByNameAsync("absent", fixture.Token));
    }

    [Fact]
    public async Task ReplaceByNameDeletesEverySameNameRowAndKeepsUnrelatedOnes()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) },
            fixture.Token);
        var second = await store.InsertAsync(Row("demo", "2.0", new string('b', 64)),
            new Dictionary<string, string> { ["skills/b/SKILL.md"] = new string('b', 64) },
            fixture.Token);
        var other = await store.InsertAsync(Row("other", "1.0", new string('c', 64)),
            new Dictionary<string, string>(), fixture.Token);

        var third = await store.ReplaceByNameAsync(Row("demo", "3.0", new string('d', 64)),
            new Dictionary<string, string> { ["skills/c/SKILL.md"] = new string('e', 64) },
            fixture.Token);

        var rows = await store.ListAsync(fixture.Token);
        Assert.Equal(2, rows.Count);
        PluginRowAssert.Equal(third, rows[0]);
        PluginRowAssert.Equal(other, rows[1]);
        Assert.Empty(await store.ListPayloadHashesAsync(first.Id, fixture.Token));
        Assert.Empty(await store.ListPayloadHashesAsync(second.Id, fixture.Token));
        var baseline = await store.ListPayloadHashesAsync(third.Id, fixture.Token);
        Assert.Equal(new string('e', 64), baseline["skills/c/SKILL.md"]);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ReplaceByNameWithNoSameNameRowSimplyInserts()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.ReplaceByNameAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) },
            fixture.Token);
        PluginRowAssert.Equal(row, await store.GetAsync(row.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ReplaceByNameRollsBackTheDeletesWhenABaselineRowIsRejected()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) },
            fixture.Token);
        var oversized = new Dictionary<string, string> { [new string('p', 261)] = new string('a', 64) };

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() =>
            store.ReplaceByNameAsync(Row("demo", "2.0", new string('b', 64)), oversized, fixture.Token));

        PluginRowAssert.Equal(first, await store.GetAsync(first.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ReplaceByNameSchemaViolationIsAFailureAndKeepsTheOldRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
        var badSource = new InstalledPlugin(Guid.NewGuid(), "demo", "2.0", new string('b', 64),
            "demo-2-0-bbbbbbbb", InstalledPluginState.Installed, new string('s', 1100),
            DateTimeOffset.UnixEpoch, Components, Servers, PluginOverlaySource.None, 1);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            store.ReplaceByNameAsync(badSource, new Dictionary<string, string>(), fixture.Token));

        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        PluginRowAssert.Equal(first, await store.GetAsync(first.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task DeleteReturnsTheStoredRowAndRemovesIt()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);
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
            DateTimeOffset.UnixEpoch, Components, Servers, PluginOverlaySource.None, 1);
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Store().InsertAsync(badSource, new Dictionary<string, string>(), fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task InsertPersistsThePayloadHashBaselineAtomically()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var baseline = new Dictionary<string, string>
        {
            ["skills/a/SKILL.md"] = new string('a', 64),
            ["assets/logo.png"] = new string('b', 64)
        };
        var row = await store.InsertAsync(Row("demo", "1.0", new string('c', 64)), baseline, fixture.Token);

        var stored = await store.ListPayloadHashesAsync(row.Id, fixture.Token);
        Assert.Equal(2, stored.Count);
        Assert.Equal(new string('a', 64), stored["skills/a/SKILL.md"]);
        Assert.Equal(new string('b', 64), stored["assets/logo.png"]);
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
        Assert.Empty(await store.ListPayloadHashesAsync(Guid.NewGuid(), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task IdempotentReinstallKeepsTheBaselineAndFillsMissingKeys()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var first = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) }, fixture.Token);

        var again = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>
            {
                ["skills/a/SKILL.md"] = new string('f', 64),
                ["readme.txt"] = new string('b', 64)
            }, fixture.Token);

        Assert.Equal(first.Id, again.Id);
        var stored = await store.ListPayloadHashesAsync(first.Id, fixture.Token);
        Assert.Equal(new string('a', 64), stored["skills/a/SKILL.md"]);
        Assert.Equal(new string('b', 64), stored["readme.txt"]);
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ConflictingReinsertWritesNoHashRows()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) }, fixture.Token);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            store.InsertAsync(Row("demo", "1.0", new string('b', 64)),
                new Dictionary<string, string> { ["evil.txt"] = new string('e', 64) }, fixture.Token));

        Assert.Equal(PluginPackageErrorCode.InstallConflict, error.Code);
        var stored = await store.ListPayloadHashesAsync((await store.ListAsync(fixture.Token))[0].Id, fixture.Token);
        Assert.Equal(["skills/a/SKILL.md"], stored.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task HashRowViolationRollsBackThePluginRowToo()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var oversized = new Dictionary<string, string> { [new string('p', 261)] = new string('a', 64) };

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() =>
            store.InsertAsync(Row("demo", "1.0", new string('a', 64)), oversized, fixture.Token));

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task SavePayloadHashesIgnoresExistingKeys()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) }, fixture.Token);

        await store.SavePayloadHashesAsync(row.Id, new Dictionary<string, string>
        {
            ["skills/a/SKILL.md"] = new string('f', 64),
            ["readme.txt"] = new string('b', 64)
        }, fixture.Token);

        var stored = await store.ListPayloadHashesAsync(row.Id, fixture.Token);
        Assert.Equal(new string('a', 64), stored["skills/a/SKILL.md"]);
        Assert.Equal(new string('b', 64), stored["readme.txt"]);
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task DeletingThePluginRowCascadesItsPayloadHashes()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string> { ["skills/a/SKILL.md"] = new string('a', 64) }, fixture.Token);

        await store.DeleteAsync(row.Id, fixture.Token);

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
        Assert.Empty(await store.ListPayloadHashesAsync(row.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task DriftedIsReachableFromAnyStateAndRoundTrips()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var store = fixture.Store();
        var row = await store.InsertAsync(Row("demo", "1.0", new string('a', 64)),
            new Dictionary<string, string>(), fixture.Token);

        PluginRowAssert.Equal(row with { State = InstalledPluginState.Drifted, TrustRevision = 2 },
            await store.SetStateAsync(row.Id, InstalledPluginState.Drifted, fixture.Token));
        Assert.Equal(InstalledPluginState.Drifted,
            (await store.GetAsync(row.Id, fixture.Token))!.State);
        PluginRowAssert.Equal(row with { State = InstalledPluginState.Disabled, TrustRevision = 3 },
            await store.SetStateAsync(row.Id, InstalledPluginState.Disabled, fixture.Token));
    }
}
