using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Tests;

internal sealed class SqliteModelIdentityFixture(string root, SqliteConfigurationRepository repository) : IDisposable
{
    public SqliteConfigurationRepository Repository { get; } = repository;
    public AiProvider Provider { get; } = CreateProvider("fixture-provider");

    public static async Task<SqliteModelIdentityFixture> CreateAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var connections = new SqliteConnectionFactory(paths);
        var fixture = new SqliteModelIdentityFixture(root, new SqliteConfigurationRepository(connections));
        try
        {
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            await fixture.Repository.SaveProviderAsync(fixture.Provider, cancellationToken);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    public ModelProfile Model(string modelId, bool isDefault = false) =>
        new(Guid.NewGuid(), Provider.Id, modelId, modelId, 128000, 8192, "off",
            null, null, false, false, isDefault, true, "{}");

    public static AiProvider CreateProvider(string name) =>
        new(Guid.NewGuid(), name, ProviderProtocol.OpenAiResponses,
            new Uri("https://example.invalid/v1/"), "fixture-key", null, false, true,
            new Dictionary<string, string>());

    public void Dispose()
    {
        SqliteTestPool.Clear(root);
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
