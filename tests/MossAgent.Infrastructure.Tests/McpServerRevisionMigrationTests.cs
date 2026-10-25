using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class McpServerRevisionMigrationTests
{
    [Fact]
    public async Task VersionThirtyFiveProfilesSurviveWithBackfilledRevisionAndStayWritable()
    {
        await using var fixture = await McpRevisionMigrationFixture.CreateAsync();
        var saved = await fixture.Repository.SaveAsync(new McpServerProfile(Guid.NewGuid(),
            "legacy", McpTransportKind.Stdio, "legacy-command", "[\"--flag\"]", null,
            "{\"K\":\"V\"}", null, "{}", null, false), fixture.Token);
        Assert.Equal(1, saved.Revision);
        await RestoreVersionThirtyFiveAsync(fixture);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('mcp_servers') WHERE name='revision';"));
        var persisted = Assert.Single(await fixture.Repository.GetAllAsync(fixture.Token));
        Assert.Equal(saved, persisted);
        var updated = await fixture.Repository.SaveAsync(persisted with { IsEnabled = true }, fixture.Token);
        Assert.Equal(2, updated.Revision);
        Assert.True(Assert.Single(await fixture.Repository.GetAllAsync(fixture.Token)).IsEnabled);
    }

    [Fact]
    public async Task FailedMigrationRollsBackRevisionColumnBeforeAnExplicitRetry()
    {
        await using var fixture = await McpRevisionMigrationFixture.CreateAsync();
        await fixture.Repository.SaveAsync(new McpServerProfile(Guid.NewGuid(),
            "legacy", McpTransportKind.Stdio, "legacy-command", "[]", null, "{}", null, "{}",
            null, true), fixture.Token);
        await RestoreVersionThirtyFiveAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_mcp_revision_version BEFORE INSERT ON schema_versions
            WHEN NEW.version=36 BEGIN SELECT RAISE(ABORT, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(35, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('mcp_servers') WHERE name='revision';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_mcp_revision_version;");

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var persisted = Assert.Single(await fixture.Repository.GetAllAsync(fixture.Token));
        Assert.Equal(1, persisted.Revision);
        Assert.Equal("legacy-command", persisted.Command);
    }

    private static Task RestoreVersionThirtyFiveAsync(McpRevisionMigrationFixture fixture) => fixture.ExecuteAsync("""
        ALTER TABLE mcp_servers DROP COLUMN revision;
        DELETE FROM schema_versions WHERE version>=36;
        """);
}
