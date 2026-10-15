using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserSettingsViewModelTests
{
    [Fact]
    public async Task BrowserOptions_DisplayLocalizedLabelsAndKeepEnumConfiguration()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var viewModel = services.GetRequiredService<BrowserSettingsViewModel>();
            await viewModel.Initialization;
            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.Equal(["内置浏览器", "Chrome", "Edge"],
                viewModel.BrowserKinds.Select(option => option.Label));
            Assert.Equal(["隔离资料", "日常资料", "已有 CDP 会话"],
                viewModel.ProfilePreferences.Select(option => option.Label));
            Assert.Equal("内置浏览器", viewModel.SelectedBrowserKind?.Label);
            Assert.False(viewModel.IsDailyProfile);
            Assert.False(viewModel.IsExistingCdp);

            viewModel.SelectedBrowserKind = viewModel.BrowserKinds[1];
            viewModel.SelectedProfilePreference = viewModel.ProfilePreferences[1];
            viewModel.ProfileDirectory = root;
            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = await services.GetRequiredService<IBrowserConfigurationRepository>()
                .GetAsync(CancellationToken.None);
            Assert.Equal(DefaultBrowserKind.Chrome, saved.DefaultBrowser);
            Assert.Equal(BrowserProfilePreference.DailyProfile, saved.ProfilePreference);
            Assert.Equal("Chrome", viewModel.SelectedBrowserKind?.Label);
            Assert.True(viewModel.IsDailyProfile);
            Assert.False(viewModel.IsExistingCdp);

            viewModel.SelectedProfilePreference = viewModel.ProfilePreferences[2];
            Assert.False(viewModel.IsDailyProfile);
            Assert.True(viewModel.IsExistingCdp);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_RejectsEmbeddedBrowserWithExistingCdp()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IBrowserConfigurationRepository>();
            var viewModel = services.GetRequiredService<BrowserSettingsViewModel>();
            await viewModel.Initialization;
            await viewModel.RefreshCommand.ExecuteAsync(null);
            var original = await repository.GetAsync(CancellationToken.None);
            viewModel.SelectedProfilePreference = viewModel.ProfilePreferences[2];

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal(original, await repository.GetAsync(CancellationToken.None));
            Assert.Contains("内置浏览器不能", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_RejectsNonNetworkCdpAndPersistsWebSocketEndpoint()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IBrowserConfigurationRepository>();
            var viewModel = services.GetRequiredService<BrowserSettingsViewModel>();
            await viewModel.Initialization;
            await viewModel.RefreshCommand.ExecuteAsync(null);
            var original = await repository.GetAsync(CancellationToken.None);
            viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
            viewModel.ProfilePreference = BrowserProfilePreference.ExistingCdp;
            viewModel.CdpEndpoint = "file:///C:/browser/devtools";

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal(original, await repository.GetAsync(CancellationToken.None));
            Assert.Contains("HTTP(S)", viewModel.Status);

            viewModel.CdpEndpoint = "https://example.com/devtools/browser/test";
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal(original, await repository.GetAsync(CancellationToken.None));
            Assert.Contains("本机", viewModel.Status);

            viewModel.CdpEndpoint = "http://user:secret@127.0.0.1:9222";
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal(original, await repository.GetAsync(CancellationToken.None));
            Assert.DoesNotContain("secret", viewModel.Status);

            viewModel.CdpEndpoint = "ws://127.0.0.1:9222/devtools/browser/test";
            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = await repository.GetAsync(CancellationToken.None);
            Assert.Equal(DefaultBrowserKind.Chrome, saved.DefaultBrowser);
            Assert.Equal(BrowserProfilePreference.ExistingCdp, saved.ProfilePreference);
            Assert.Equal(new Uri(viewModel.CdpEndpoint), saved.CdpEndpoint);
            Assert.Contains("已保存", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_ReportsDatabaseFailureWithoutExposingDetailsOrReplacingSettings()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            var repository = services.GetRequiredService<IBrowserConfigurationRepository>();
            var original = await repository.GetAsync(cancellationToken);
            await repository.SaveAsync(original, cancellationToken);
            var viewModel = services.GetRequiredService<BrowserSettingsViewModel>();
            await viewModel.Initialization;
            await viewModel.RefreshCommand.ExecuteAsync(null);
            await ExecuteSqlAsync(services, """
                CREATE TRIGGER deny_browser_update BEFORE UPDATE ON browser_configuration
                BEGIN SELECT RAISE(ABORT, 'secret-token'); END;
                """, cancellationToken);

            viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Contains("无法保存", viewModel.Status);
            Assert.DoesNotContain("secret-token", viewModel.Status);
            Assert.Equal(original, await repository.GetAsync(cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshCommand_ReportsDatabaseFailureWithoutClearingCurrentSelection()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            var viewModel = services.GetRequiredService<BrowserSettingsViewModel>();
            await viewModel.Initialization;
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
            await ExecuteSqlAsync(services, "DROP TABLE browser_configuration;", cancellationToken);

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.Contains("无法加载", viewModel.Status);
            Assert.DoesNotContain("SQLite", viewModel.Status);
            Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task ExecuteSqlAsync(
        ServiceProvider services, string sql, CancellationToken cancellationToken)
    {
        var connections = services.GetRequiredService<SqliteConnectionFactory>();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string root)
    {
        var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>()
            .InitializeAsync(CancellationToken.None);
        return services;
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
