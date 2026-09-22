using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConfigurationRepositoryTests
{
    [Fact]
    public async Task SaveDefaultModel_ClearsPreviousDefaultForProvider()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            var repository = new SqliteConfigurationRepository(connections);
            var provider = CreateProvider();
            await repository.SaveProviderAsync(provider, cancellationToken);
            await repository.SaveModelAsync(CreateModel(provider.Id, "first", true), cancellationToken);
            await repository.SaveModelAsync(CreateModel(provider.Id, "second", true), cancellationToken);

            var models = await repository.GetModelsAsync(provider.Id, cancellationToken);

            var model = Assert.Single(models, static item => item.IsDefault);
            Assert.Equal("second", model.ModelId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static AiProvider CreateProvider() =>
        new(
            Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.invalid/v1/"), "key", null, true, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(Guid providerId, string id, bool isDefault) =>
        new(
            Guid.NewGuid(), providerId, id, id, 128000, 8192, "medium",
            null, null, false, false, isDefault, true, "{}");

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
