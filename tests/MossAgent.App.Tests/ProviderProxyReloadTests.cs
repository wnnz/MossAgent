using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderProxyReloadTests
{
    [Fact]
    public async Task EarlierReload_CannotOverwriteNewerProxyNames()
    {
        var (repository, settings, proxy) = CreateSettings();
        var delayed = DelayedSnapshot();
        repository.ProxyLoader = () => delayed.Task;
        var earlier = settings.ReloadProxiesAsync();
        repository.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([proxy with { Name = "newer" }]);

        await settings.ReloadProxiesAsync();
        delayed.SetResult([proxy]);
        await earlier;

        Assert.Equal("newer", settings.SelectedProxy?.Name);
        Assert.Equal("draft", settings.Name);
    }

    [Fact]
    public async Task EarlierFailure_CannotReplaceTheLatestSuccessfulStatus()
    {
        var (repository, settings, proxy) = CreateSettings();
        var delayed = DelayedSnapshot();
        repository.ProxyLoader = () => delayed.Task;
        var earlier = settings.ReloadProxiesAsync();
        repository.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([proxy]);
        await settings.ReloadProxiesAsync();

        delayed.SetException(new InvalidOperationException("fixture-secret-error"));
        await earlier;

        Assert.False(settings.HasValidationError);
        Assert.DoesNotContain("fixture-secret", settings.Status);
    }

    [Fact]
    public async Task FailedRead_RetainsDraftButBlocksSavingAndRedactsError()
    {
        var (repository, settings, proxy) = CreateSettings();
        var selected = settings.SelectedProxy;
        repository.ProxyLoader = () => Task.FromException<IReadOnlyList<ProxyProfile>>(
            new InvalidOperationException("fixture-password-and-api-key"));

        await settings.ReloadProxiesAsync();
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.Same(selected, settings.SelectedProxy);
        Assert.Empty(repository.Providers);
        Assert.True(settings.IsFormOpen);
        Assert.Equal("draft", settings.Name);
        Assert.Equal("draft-fixture-key", settings.ApiKey);
        Assert.Contains("无法读取代理配置", settings.Status);
        Assert.DoesNotContain("fixture-password", settings.Status);
        repository.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([proxy]);
        await settings.ReloadProxiesAsync();
        Assert.False(settings.HasValidationError);
    }

    [Fact]
    public async Task ReplacingOptions_CannotEraseTheDesiredIdentityThroughTransientNullSelection()
    {
        var (_, settings, proxy) = CreateSettings();
        settings.Proxies.CollectionChanged += (_, _) => settings.SelectedProxy = null;

        await settings.ReloadProxiesAsync();

        Assert.Equal(proxy.Id, settings.SelectedProxy?.Id);
    }

    [Fact]
    public async Task AChoiceMadeDuringReload_IsPreservedInsteadOfRestoringTheEarlierChoice()
    {
        var (repository, settings, proxy) = CreateSettings();
        var delayed = DelayedSnapshot();
        repository.ProxyLoader = () => delayed.Task;
        var reload = settings.ReloadProxiesAsync();
        settings.SelectedProxy = Assert.Single(settings.Proxies, option => option.Id is null);

        delayed.SetResult([proxy]);
        await reload;

        Assert.NotNull(settings.SelectedProxy);
        Assert.Null(settings.SelectedProxy.Id);
    }

    [Fact]
    public async Task MissingSavedChoice_DoesNotBecomeAnAlternativeAndReturnsWhenAvailable()
    {
        var (repository, settings, proxy) = CreateSettings();
        var missingId = Guid.NewGuid();
        var provider = new AiProvider(Guid.NewGuid(), "saved", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "fixture", missingId, false, true, new Dictionary<string, string>());
        repository.Providers.Add(provider);
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.EditProviderCommand.Execute(provider);

        await settings.ReloadProxiesAsync();
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.Null(settings.SelectedProxy);
        Assert.Equal(missingId, Assert.Single(repository.Providers).ProxyId);
        repository.Proxies.Add(proxy with { Id = missingId, IsDefault = false });
        await settings.ReloadProxiesAsync();
        Assert.Equal(missingId, settings.SelectedProxy?.Id);
    }

    [Fact]
    public async Task RefreshingValidProxies_DoesNotClearUnrelatedFormValidation()
    {
        var (_, settings, _) = CreateSettings();
        settings.Name = string.Empty;
        await settings.SaveCommand.ExecuteAsync(null);
        var status = settings.Status;

        await settings.ReloadProxiesAsync();

        Assert.True(settings.HasValidationError);
        Assert.Equal(status, settings.Status);
    }

    [Fact]
    public async Task Save_CannotPersistAnUnvalidatedChoiceChangedDuringRepositoryRead()
    {
        var (repository, settings, proxy) = CreateSettings();
        var delayed = DelayedSnapshot();
        repository.ProxyLoader = () => delayed.Task;
        var save = settings.SaveCommand.ExecuteAsync(null);
        settings.SelectedProxy = Assert.Single(settings.Proxies, option => option.Id is null);

        delayed.SetResult([proxy]);
        await save;

        Assert.Empty(repository.Providers);
        Assert.True(settings.HasValidationError);
        Assert.Contains("选择已变化", settings.Status);
    }

    private static TaskCompletionSource<IReadOnlyList<ProxyProfile>> DelayedSnapshot() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static (ProxyReloadProbeRepository Repository, ProviderSettingsViewModel Settings, ProxyProfile Proxy) CreateSettings()
    {
        var repository = new ProxyReloadProbeRepository();
        var proxy = new ProxyProfile(Guid.NewGuid(), "selected", ProxyProtocol.Http,
            "127.0.0.1", 8080, null, null, true, true);
        repository.Proxies.Add(proxy);
        var settings = new ProviderSettingsViewModel(repository);
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "draft";
        settings.ApiKey = "draft-fixture-key";
        return (repository, settings, proxy);
    }
}
