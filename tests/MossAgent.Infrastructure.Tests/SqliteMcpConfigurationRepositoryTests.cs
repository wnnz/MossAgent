using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteMcpConfigurationRepositoryTests
{
    [Fact]
    public Task SaveNew_StartsAtRevisionOneAndIncrementsOnEverySave() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var saved = await repository.SaveAsync(CreateProfile("server"), token);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(saved, Assert.Single(await repository.GetAllAsync(token)));

        var updated = await repository.SaveAsync(saved with { IsEnabled = false }, token);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(updated, Assert.Single(await repository.GetAllAsync(token)));

        var third = await repository.SaveAsync(updated with { IsEnabled = true }, token);
        Assert.Equal(3, third.Revision);
        Assert.True(Assert.Single(await repository.GetAllAsync(token)).IsEnabled);
    });

    [Fact]
    public Task StaleRevision_ThrowsConflictWithoutOverwriting() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var saved = await repository.SaveAsync(CreateProfile("server"), token);
        var newer = await repository.SaveAsync(saved with { Command = "newer-command" }, token);
        Assert.Equal(2, newer.Revision);

        await Assert.ThrowsAsync<McpConfigurationConflictException>(
            () => repository.SaveAsync(saved with { Command = "stale-command" }, token));

        var persisted = Assert.Single(await repository.GetAllAsync(token));
        Assert.Equal("newer-command", persisted.Command);
        Assert.Equal(2, persisted.Revision);
    });

    [Fact]
    public Task UnsavedProfileForExistingId_ThrowsConflictInsteadOfOverwriting() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var profile = CreateProfile("server");
        var saved = await repository.SaveAsync(profile, token);
        Assert.Equal(1, saved.Revision);

        await Assert.ThrowsAsync<McpConfigurationConflictException>(
            () => repository.SaveAsync(profile with { Command = "conflicting" }, token));

        Assert.Equal(saved, Assert.Single(await repository.GetAllAsync(token)));
    });

    [Fact]
    public Task DeletedProfile_SaveWithStaleRevisionThrowsConflict() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var saved = await repository.SaveAsync(CreateProfile("server"), token);
        await repository.DeleteAsync(saved.Id, token);
        Assert.Empty(await repository.GetAllAsync(token));

        await Assert.ThrowsAsync<McpConfigurationConflictException>(
            () => repository.SaveAsync(saved with { Command = "revived" }, token));

        Assert.Empty(await repository.GetAllAsync(token));
    });

    [Fact]
    public Task RevisionSaveForMissingId_ThrowsConflictWithoutInserting() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<McpConfigurationConflictException>(
            () => repository.SaveAsync(CreateProfile("missing") with { Revision = 5 }, token));

        Assert.Empty(await repository.GetAllAsync(token));
    });

    [Fact]
    public Task ConcurrentSaves_OnlyOneWriterWins() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var saved = await repository.SaveAsync(CreateProfile("server"), token);

        var first = SaveCatchingAsync(repository, saved with { Command = "first" }, token);
        var second = SaveCatchingAsync(repository, saved with { Command = "second" }, token);
        var outcomes = await Task.WhenAll(first, second);

        Assert.Equal(1, outcomes.Count(outcome => outcome is not null));
        var persisted = Assert.Single(await repository.GetAllAsync(token));
        Assert.Equal(2, persisted.Revision);
        Assert.Contains(persisted.Command, new[] { "first", "second" });
    });

    [Fact]
    public Task Read_RoundTripsEveryFieldIncludingRevision() => RunAsync(async repository =>
    {
        var token = TestContext.Current.CancellationToken;
        var profile = new McpServerProfile(Guid.NewGuid(), "full", McpTransportKind.StreamableHttp,
            null, "[\"a\"]", null, "{\"K\":\"V\"}", new Uri("https://example.test/mcp"),
            "{\"Authorization\":\"Bearer x\"}", null, false);
        var saved = await repository.SaveAsync(profile, token);

        var persisted = Assert.Single(await repository.GetAllAsync(token));
        Assert.Equal(saved, persisted);
        Assert.Equal(profile with { Revision = 1 }, persisted);
    });

    private static async Task<McpServerProfile?> SaveCatchingAsync(
        IMcpConfigurationRepository repository, McpServerProfile profile, CancellationToken token)
    {
        try
        {
            return await repository.SaveAsync(profile, token);
        }
        catch (McpConfigurationConflictException)
        {
            return null;
        }
    }

    private static McpServerProfile CreateProfile(string name) => new(
        Guid.NewGuid(), name, McpTransportKind.Stdio, "fixture-command",
        "[]", null, "{}", null, "{}", null, true);

    private static async Task RunAsync(Func<SqliteMcpConfigurationRepository, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections)
                .InitializeAsync(TestContext.Current.CancellationToken);
            await action(new SqliteMcpConfigurationRepository(connections));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
