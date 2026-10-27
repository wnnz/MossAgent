using MossAgent.Application.Plugins;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Temp plugin root plus a real migrated SQLite database for install tests.</summary>
internal sealed class PluginInstallFixture : IAsyncDisposable
{
    private readonly PluginPackageFixture _packages = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.PluginInstall",
        Guid.NewGuid().ToString("N"));

    private PluginInstallFixture()
    {
        var paths = new AppDataPaths(_root);
        Connections = new SqliteConnectionFactory(paths);
        Initializer = new SqliteDatabaseInitializer(paths, Connections);
    }

    public CancellationToken Token => TestContext.Current.CancellationToken;
    public PluginPackageFixture Packages => _packages;
    public SqliteConnectionFactory Connections { get; }
    public SqliteDatabaseInitializer Initializer { get; }
    public ManualToolTimeProvider Time { get; } = new();
    public string PluginRoot => Path.Combine(_root, "plugins");
    public string StagingRoot => Path.Combine(PluginRoot, PluginPackageInstaller.StagingDirectoryName);

    public static async Task<PluginInstallFixture> CreateAsync()
    {
        var fixture = new PluginInstallFixture();
        try
        {
            await fixture.Initializer.InitializeAsync(fixture.Token);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public SqliteInstalledPluginStore Store() => new(Connections);

    public PluginPackageInstaller Installer(IInstalledPluginStore? store = null,
        IPluginPackagePreviewer? previewer = null) =>
        new(previewer ?? new ZipPluginPackagePreviewer(), store ?? Store(), PluginRoot, Time);

    public async Task<long> CountAsync(string sql)
    {
        await using var connection = Connections.Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token));
    }

    public IReadOnlyList<string> StagingEntries() =>
        Directory.Exists(StagingRoot) ? [.. Directory.EnumerateFileSystemEntries(StagingRoot)] : [];

    public async ValueTask DisposeAsync()
    {
        SqliteTestPool.Clear(_root);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        _packages.Dispose();
    }
}
