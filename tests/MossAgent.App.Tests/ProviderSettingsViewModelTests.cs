using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderSettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_ReportsMissingNameSeparatelyFromInvalidBaseUrl()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal("请输入提供商名称。", viewModel.Status);
            Assert.True(viewModel.HasValidationError);
            Assert.True(viewModel.IsFormOpen);
            Assert.Empty(await repository.GetProvidersAsync(cancellationToken));

            viewModel.CloseFormCommand.Execute(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.Empty(viewModel.Status);
            Assert.False(viewModel.HasValidationError);

            viewModel.Name = "example";
            viewModel.BaseUrl = "ftp://models.example.test/v1";
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Contains("HTTP/HTTPS Base URL", viewModel.Status);
            Assert.True(viewModel.HasValidationError);

            viewModel.BaseUrl = "https://models.example.test/v1";
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal("example", Assert.Single(await repository.GetProvidersAsync(cancellationToken)).Name);
            Assert.False(viewModel.HasValidationError);
            Assert.False(viewModel.IsFormOpen);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_PersistsProviderAndSelectedProxy()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            await AddProxyAsync(services.GetRequiredService<ProxySettingsViewModel>());
            var proxy = Assert.Single(await repository.GetProxiesAsync(CancellationToken.None));
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.False(viewModel.HasSelectedProvider);
            Assert.Equal("输入 API Key；允许匿名访问时可留空", viewModel.ApiKeyPlaceholder);
            Assert.Equal("删除", viewModel.DeleteButtonText);
            Assert.False(viewModel.DeleteCommand.CanExecute(null));
            viewModel.Name = "gemini-test";
            viewModel.BaseUrl = "http://127.0.0.1:5000/v1beta";
            viewModel.ApiKey = "test-key";
            viewModel.SelectedProtocolOption = viewModel.ProtocolOptions.Single(
                option => option.Protocol == ProviderProtocol.GeminiGenerateContent);
            viewModel.HeadersJson = "{\"x-test\":\"value\"}";
            viewModel.SelectedProxy = Assert.Single(viewModel.Proxies, item => item.Id == proxy.Id);
            Assert.Equal("Gemini generateContent API", viewModel.SelectedProtocolOption.DisplayName);

            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(await repository.GetProvidersAsync(CancellationToken.None));
            Assert.Equal("gemini-test", saved.Name);
            Assert.Equal(new Uri("http://127.0.0.1:5000/v1beta"), saved.BaseUri);
            Assert.Equal("test-key", saved.ApiKey);
            Assert.Equal(ProviderProtocol.GeminiGenerateContent, saved.Protocol);
            Assert.Equal(proxy.Id, saved.ProxyId);
            Assert.Equal("value", saved.Headers["x-test"]);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("ftp://models.example.test/v1")]
    [InlineData("file:///models.json")]
    [InlineData("https://user:password@models.example.test/v1")]
    public async Task SaveCommand_RejectsUnsupportedOrCredentialedBaseUrls(string baseUrl)
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "invalid-provider";
            viewModel.BaseUrl = baseUrl;

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(await repository.GetProvidersAsync(CancellationToken.None));
            Assert.True(viewModel.IsFormOpen);
            Assert.Contains("HTTP/HTTPS", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_EmptyApiKeyPreservesExistingSecret()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var existing = new AiProvider(
                Guid.NewGuid(), "existing", ProviderProtocol.OpenAiCompatible,
                new Uri("https://api.example.test/v1"), "saved-test-key", null,
                false, true, new Dictionary<string, string>());
            await repository.SaveProviderAsync(existing, CancellationToken.None);
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.EditProviderCommand.Execute(existing);
            Assert.True(viewModel.HasSelectedProvider);
            Assert.Equal("留空保留原密钥", viewModel.ApiKeyPlaceholder);
            Assert.Equal("删除", viewModel.DeleteButtonText);
            await viewModel.DeleteCommand.ExecuteAsync(null);
            Assert.Equal("确认删除", viewModel.DeleteButtonText);
            viewModel.CloseFormCommand.Execute(null);
            Assert.Equal("删除", viewModel.DeleteButtonText);
            viewModel.OpenEditFormCommand.Execute(null);
            await viewModel.DeleteCommand.ExecuteAsync(null);
            Assert.Equal("确认删除", viewModel.DeleteButtonText);
            viewModel.ApiKey = string.Empty;

            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(await repository.GetProvidersAsync(CancellationToken.None));
            Assert.Equal("saved-test-key", saved.ApiKey);
            Assert.Equal(existing.Id, saved.Id);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_DoesNotSilentlySwitchDisabledProxyToDirect()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var proxy = new ProxyProfile(Guid.NewGuid(), "disabled", ProxyProtocol.Http,
                "127.0.0.1", 8080, null, null, false, false);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://models.example.test/v1/"), "fixture-key", proxy.Id,
                false, true, new Dictionary<string, string>());
            await repository.SaveProviderAsync(provider, cancellationToken);
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.EditProviderCommand.Execute(provider);

            Assert.Null(viewModel.SelectedProxy);
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal(proxy.Id, Assert.Single(await repository.GetProvidersAsync(cancellationToken)).ProxyId);
            Assert.Contains("代理", viewModel.Status);

            viewModel.SelectedProxy = Assert.Single(viewModel.Proxies, item => item.Id is null);
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Null(Assert.Single(await repository.GetProvidersAsync(cancellationToken)).ProxyId);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_RejectsProxyDisabledWhileFormIsOpen()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var proxy = new ProxyProfile(Guid.NewGuid(), "selected", ProxyProtocol.Http,
                "127.0.0.1", 8080, null, null, false, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "fixture";
            viewModel.SelectedProxy = Assert.Single(viewModel.Proxies, item => item.Id == proxy.Id);
            await repository.SaveProxyAsync(proxy with { IsEnabled = false }, cancellationToken);

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(await repository.GetProvidersAsync(cancellationToken));
            Assert.True(viewModel.IsFormOpen);
            Assert.Contains("代理", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NewProvider_PrefersEnabledDefaultProxyButAllowsExplicitDirectConnection()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var proxy = new ProxyProfile(Guid.NewGuid(), "default", ProxyProtocol.Http,
                "127.0.0.1", 8080, null, null, true, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);

            Assert.Equal(proxy.Id, viewModel.SelectedProxy?.Id);
            viewModel.SelectedProxy = Assert.Single(viewModel.Proxies, item => item.Id is null);
            viewModel.Name = "direct";
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Null(Assert.Single(await repository.GetProvidersAsync(cancellationToken)).ProxyId);

            await repository.SaveProxyAsync(proxy with { IsEnabled = false }, cancellationToken);
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.Null(viewModel.SelectedProxy?.Id);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AddProxyAsync(ProxySettingsViewModel viewModel)
    {
        await viewModel.RefreshCommand.ExecuteAsync(null);
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "test-proxy";
        viewModel.Host = "127.0.0.1";
        viewModel.Port = 8080;
        viewModel.Protocol = ProxyProtocol.Http;
        await viewModel.SaveCommand.ExecuteAsync(null);
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
