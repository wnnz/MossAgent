using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserEditorCatalogTests
{
    [Fact]
    public async Task CleanRefresh_UpdatesSavedValuesAndCanonicalProxyReference()
    {
        var proxyId = Guid.NewGuid();
        var configurations = Configurations(proxyId);
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, configurations);
        repository.Configuration = repository.Configuration with { DefaultBrowser = DefaultBrowserKind.Edge, ProxyId = proxyId };

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(DefaultBrowserKind.Edge, viewModel.DefaultBrowser);
        Assert.Same(viewModel.Proxies.Single(proxy => proxy.Id == proxyId), viewModel.SelectedProxy);
        Assert.False(viewModel.IsProxyUnavailable);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ProxyNotification_PreservesDraftAndUnavailableProxyIdentity()
    {
        var proxyId = Guid.NewGuid();
        var configurations = Configurations(proxyId);
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, configurations);
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        viewModel.SelectedProxy = viewModel.Proxies.Single(proxy => proxy.Id == proxyId);
        configurations.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([]);

        await viewModel.ReloadProxiesAsync();

        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        Assert.Equal(proxyId, viewModel.SelectedProxy?.Id);
        Assert.True(viewModel.IsProxyUnavailable);
        Assert.True(viewModel.HasEditorNotice);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Contains("直连", viewModel.EditorNotice);
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task DisabledProxy_IsNotAcceptedAtSaveTime()
    {
        var proxyId = Guid.NewGuid();
        var configurations = Configurations(proxyId);
        using var viewModel = new BrowserSettingsViewModel(new BrowserSettingsProbeRepository(), configurations);
        viewModel.SelectedProxy = viewModel.Proxies.Single(proxy => proxy.Id == proxyId);
        configurations.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>(
            [new(proxyId, "disabled", ProxyProtocol.Http, "127.0.0.1", 7890, null, null, false, false)]);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsProxyUnavailable);
        Assert.True(viewModel.HasStatusError);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedWrite_UsesLocalizedErrorWithoutLeakingSecrets()
    {
        var repository = new BrowserSettingsProbeRepository
        {
            BeforeWrite = _ => throw new IOException("synthetic-key-proxy-password")
        };
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasStatusError);
        Assert.Contains("无法保存", viewModel.Status);
        Assert.DoesNotContain("synthetic-key", viewModel.Status);
        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidEnumDraft_IsRejectedBeforeStorage(bool profile)
    {
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        if (profile) viewModel.ProfilePreference = (BrowserProfilePreference)999;
        else viewModel.DefaultBrowser = (DefaultBrowserKind)999;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(repository.Writes);
        Assert.True(viewModel.HasStatusError);
        Assert.Contains("无效", viewModel.Status);
    }

    private static ConfigurationMutationProbeRepository Configurations(Guid proxyId) => new()
    {
        ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>(
            [new(proxyId, "synthetic-proxy", ProxyProtocol.Http, "127.0.0.1", 7890, null, null, false, true)])
    };
}
