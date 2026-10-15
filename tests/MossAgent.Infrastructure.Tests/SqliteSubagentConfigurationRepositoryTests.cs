using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteSubagentConfigurationRepositoryTests
{
    [Fact]
    public async Task SaveAndGet_RoundTripsAndMigrationRemainsIdempotent()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            var initializer = new SqliteDatabaseInitializer(paths, connections);
            await initializer.InitializeAsync(cancellationToken);
            var repository = new SqliteSubagentConfigurationRepository(connections);
            var profile = CreateProfile("review", true);

            await repository.SaveAsync(profile, cancellationToken);
            await initializer.InitializeAsync(cancellationToken);
            var restored = Assert.Single(await repository.GetAllAsync(cancellationToken));

            Assert.Equal(profile, restored);
            await repository.SaveAsync(profile with { ShareContext = false }, cancellationToken);
            Assert.False(Assert.Single(await repository.GetAllAsync(cancellationToken)).ShareContext);
            await repository.DeleteAsync(profile.Id, cancellationToken);
            Assert.Empty(await repository.GetAllAsync(cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static SubagentProfile CreateProfile(string name, bool shareContext) =>
        new(Guid.NewGuid(), name, "独立审查", "分析并汇报。", Guid.NewGuid(),
            Guid.NewGuid(), "high", shareContext, true);

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
