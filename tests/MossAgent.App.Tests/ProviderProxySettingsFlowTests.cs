using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderProxySettingsFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProxyAdded_UpdatesChoicesWithoutResettingProviderDraft(bool editing) => RunAsync(async (window, repository) =>
    {
        var provider = new AiProvider(Guid.NewGuid(), "stored", ProviderProtocol.OpenAiResponses,
            new Uri("https://stored.example.test/"), "saved-fixture-key", null, false, true, new Dictionary<string, string>());
        if (editing)
        {
            await repository.SaveProviderAsync(provider, TestContext.Current.CancellationToken);
            await window.Providers.RefreshCommand.ExecuteAsync(null);
            window.Providers.EditProviderCommand.Execute(provider);
        }
        else window.Providers.OpenCreateFormCommand.Execute(null);
        SetDraft(window.Providers);
        window.Providers.SelectedProxy = Assert.Single(window.Providers.Proxies);
        var status = window.Providers.Status;

        await AddProxyAsync(window.Proxies, "added", isDefault: true);

        Assert.Contains(window.Providers.Proxies, item => item.Name == "added");
        Assert.Null(window.Providers.SelectedProxy?.Id);
        Assert.NotNull(window.Providers.SelectedProxy);
        Assert.Equal(status, window.Providers.Status);
        Assert.Equal(editing ? provider.Id : (Guid?)null, window.Providers.SelectedItem?.Id);
        AssertDraft(window.Providers);
    });

    [Fact]
    public Task RenamedProxy_UpdatesTheSelectedSnapshotAndRetainsDraft() => RunAsync(async (window, _) =>
    {
        await AddProxyAsync(window.Proxies, "initial");
        window.Providers.OpenCreateFormCommand.Execute(null);
        SetDraft(window.Providers);
        var selected = Assert.Single(window.Providers.Proxies, item => item.Id is not null);
        window.Providers.SelectedProxy = selected;
        window.Proxies.OpenEditFormCommand.Execute(Assert.Single(window.Proxies.Items));
        window.Proxies.Name = "renamed";

        await window.Proxies.SaveCommand.ExecuteAsync(null);

        Assert.Equal(selected.Id, window.Providers.SelectedProxy?.Id);
        Assert.Equal("renamed", window.Providers.SelectedProxy?.Name);
        Assert.Same(window.Providers.SelectedProxy, Assert.Single(window.Providers.Proxies, item => item.Id == selected.Id));
        AssertDraft(window.Providers);
    });

    [Fact]
    public Task DefaultChanges_PreserveExplicitDirectButNewFormsPreferTheLatestDefault() => RunAsync(async (window, _) =>
    {
        await AddProxyAsync(window.Proxies, "first", isDefault: true);
        window.Providers.OpenCreateFormCommand.Execute(null);
        Assert.Equal("first", window.Providers.SelectedProxy?.Name);
        window.Providers.SelectedProxy = Assert.Single(window.Providers.Proxies, item => item.Id is null);

        await AddProxyAsync(window.Proxies, "second", isDefault: true);

        Assert.NotNull(window.Providers.SelectedProxy);
        Assert.Null(window.Providers.SelectedProxy.Id);
        window.Providers.OpenCreateFormCommand.Execute(null);
        Assert.Equal("second", window.Providers.SelectedProxy?.Name);
    });

    [Fact]
    public Task DisabledChoice_IsNotSubstitutedAndReturnsAfterReenable() => RunAsync(async (window, repository) =>
    {
        await AddProxyAsync(window.Proxies, "selected", isDefault: true);
        window.Providers.OpenCreateFormCommand.Execute(null);
        SetDraft(window.Providers);
        var id = window.Providers.SelectedProxy!.Id;
        window.Proxies.OpenEditFormCommand.Execute(Assert.Single(window.Proxies.Items));
        window.Proxies.IsEnabled = false;

        await window.Proxies.SaveCommand.ExecuteAsync(null);

        Assert.Null(window.Providers.SelectedProxy);
        Assert.DoesNotContain(window.Providers.Proxies, item => item.Id == id);
        Assert.True(window.Providers.HasValidationError);
        await window.Providers.SaveCommand.ExecuteAsync(null);
        Assert.Empty(await repository.GetProvidersAsync(TestContext.Current.CancellationToken));
        AssertDraft(window.Providers);
        window.Proxies.OpenEditFormCommand.Execute(Assert.Single(window.Proxies.Items));
        window.Proxies.IsEnabled = true;
        await window.Proxies.SaveCommand.ExecuteAsync(null);
        Assert.Equal(id, window.Providers.SelectedProxy?.Id);
        Assert.False(window.Providers.HasValidationError);
        AssertDraft(window.Providers);
    });

    [Fact]
    public Task DeletedUnsavedChoice_DoesNotBecomeDirectOrPersist() => RunAsync(async (window, repository) =>
    {
        await AddProxyAsync(window.Proxies, "selected", isDefault: true);
        window.Providers.OpenCreateFormCommand.Execute(null);
        SetDraft(window.Providers);
        window.Proxies.SelectedItem = Assert.Single(window.Proxies.Items);

        await window.Proxies.DeleteCommand.ExecuteAsync(null);
        await window.Providers.SaveCommand.ExecuteAsync(null);

        Assert.Single(window.Providers.Proxies);
        Assert.Null(window.Providers.SelectedProxy);
        Assert.True(window.Providers.HasValidationError);
        Assert.Empty(await repository.GetProvidersAsync(TestContext.Current.CancellationToken));
        AssertDraft(window.Providers);
    });

    [Fact]
    public Task ReferencedProxyDeletion_IsRejectedWithoutChangingProviderRouting() => RunAsync(async (window, repository) =>
    {
        await AddProxyAsync(window.Proxies, "selected", isDefault: true);
        var proxy = Assert.Single(window.Proxies.Items);
        var provider = new AiProvider(Guid.NewGuid(), "stored", ProviderProtocol.OpenAiResponses,
            new Uri("https://stored.example.test/"), "fixture-key", proxy.Id, false, true, new Dictionary<string, string>());
        await repository.SaveProviderAsync(provider, TestContext.Current.CancellationToken);
        window.Proxies.SelectedItem = proxy;

        await window.Proxies.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(proxy.Id, Assert.Single(await repository.GetProxiesAsync(TestContext.Current.CancellationToken)).Id);
        Assert.Equal(proxy.Id, Assert.Single(await repository.GetProvidersAsync(TestContext.Current.CancellationToken)).ProxyId);
        Assert.Contains("使用", window.Proxies.Status);
    });

    [Fact]
    public Task ProxyRefresh_PropagatesExternalConfigurationChanges() => RunAsync(async (window, repository) =>
    {
        await AddProxyAsync(window.Proxies, "old");
        var proxy = Assert.Single(window.Proxies.Items);
        window.Providers.OpenCreateFormCommand.Execute(null);
        SetDraft(window.Providers);
        window.Providers.SelectedProxy = Assert.Single(window.Providers.Proxies, item => item.Id == proxy.Id);
        await repository.SaveProxyAsync(proxy with { Name = "external" }, TestContext.Current.CancellationToken);

        await window.Proxies.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("external", window.Providers.SelectedProxy?.Name);
        AssertDraft(window.Providers);
    });

    [Fact]
    public Task DisposedWindow_RejectsProxyWritesAndDoesNotRefreshFromExternalChanges() => RunAsync(async (window, repository) =>
    {
        window.Dispose();
        await AddProxyAsync(window.Proxies, "after-disposal");

        Assert.Empty(await repository.GetProxiesAsync(TestContext.Current.CancellationToken));
        Assert.False(window.Proxies.SaveCommand.CanExecute(null));
        Assert.False(window.Proxies.RefreshCommand.CanExecute(null));
        await repository.SaveProxyAsync(new ProxyProfile(Guid.NewGuid(), "external-proxy", ProxyProtocol.Http,
            "127.0.0.1", 8080, null, null, false, true), TestContext.Current.CancellationToken);
        await window.Proxies.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(window.Proxies.Items);
        Assert.DoesNotContain(window.Providers.Proxies, item => item.Name == "after-disposal");
        Assert.DoesNotContain(window.Providers.Proxies, item => item.Name == "external-proxy");
    });

    private static void SetDraft(ProviderSettingsViewModel providers)
    {
        providers.Name = "provider-draft";
        providers.ApiKey = "draft-fixture-key";
        providers.BaseUrl = "https://draft.example.test/v1/";
        providers.Protocol = ProviderProtocol.GeminiGenerateContent;
        providers.HeadersJson = "{\"x-fixture\":\"draft\"}";
        providers.IsDefault = true;
        providers.IsEnabled = false;
    }

    private static void AssertDraft(ProviderSettingsViewModel providers)
    {
        Assert.True(providers.IsFormOpen);
        Assert.Equal("provider-draft", providers.Name);
        Assert.Equal("draft-fixture-key", providers.ApiKey);
        Assert.Equal("https://draft.example.test/v1/", providers.BaseUrl);
        Assert.Equal(ProviderProtocol.GeminiGenerateContent, providers.Protocol);
        Assert.Equal("{\"x-fixture\":\"draft\"}", providers.HeadersJson);
        Assert.True(providers.IsDefault);
        Assert.False(providers.IsEnabled);
    }

    private static async Task AddProxyAsync(ProxySettingsViewModel proxies, string name, bool isDefault = false)
    {
        proxies.OpenCreateFormCommand.Execute(null);
        proxies.Name = name;
        proxies.Host = "127.0.0.1";
        proxies.Port = 8080;
        proxies.IsDefault = isDefault;
        await proxies.SaveCommand.ExecuteAsync(null);
    }

    private static async Task RunAsync(Func<MainWindowViewModel, IConfigurationRepository, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var window = services.GetRequiredService<MainWindowViewModel>();
            await window.Proxies.RefreshCommand.ExecuteAsync(null);
            await window.Providers.RefreshCommand.ExecuteAsync(null);
            await action(window, services.GetRequiredService<IConfigurationRepository>());
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
