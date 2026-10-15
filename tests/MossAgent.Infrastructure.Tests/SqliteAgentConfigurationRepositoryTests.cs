using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteAgentConfigurationRepositoryTests
{
    [Fact]
    public async Task SaveAndDelete_RoundTripAcrossRepeatedMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            var initializer = new SqliteDatabaseInitializer(paths, connections);
            await initializer.InitializeAsync(cancellationToken);
            var repository = new SqliteAgentConfigurationRepository(connections);
            var profile = new AgentProfile(Guid.NewGuid(), "审查代理", "只审查代码。",
                Guid.NewGuid(), Guid.NewGuid(), "high", true);

            await repository.SaveAsync(profile, cancellationToken);
            await initializer.InitializeAsync(cancellationToken);
            Assert.Equal(profile, Assert.Single(await repository.GetAllAsync(cancellationToken)));

            await repository.SaveAsync(profile with { IsEnabled = false }, cancellationToken);
            Assert.False(Assert.Single(await repository.GetAllAsync(cancellationToken)).IsEnabled);
            await repository.DeleteAsync(profile.Id, cancellationToken);
            Assert.Empty(await repository.GetAllAsync(cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
