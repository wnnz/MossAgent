using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderProxyReadLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoncooperativeProxyRead_IsBoundedAndPreservesTheDraftWithoutWriting(bool save)
    {
        var (repository, settings, proxy) = CreateSettings();
        using var owner = settings;
        var selected = settings.SelectedProxy;
        var delayed = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ProxyLoader = () => delayed.Task;
        var pending = save ? settings.SaveCommand.ExecuteAsync(null) : settings.ReloadProxiesAsync();
        try
        {
            await pending.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.True(repository.ProxyReadToken.IsCancellationRequested);
            Assert.Same(selected, settings.SelectedProxy);
            Assert.True(settings.IsFormOpen);
            Assert.Equal("draft", settings.Name);
            Assert.Equal("draft-fixture-key", settings.ApiKey);
            Assert.Contains("无法读取代理配置", settings.Status);
            Assert.Empty(repository.Providers);
        }
        finally
        {
            delayed.TrySetResult([proxy with { Name = "late" }]);
            await pending;
        }
        Assert.Same(selected, settings.SelectedProxy);
        repository.ProxyLoader = null;
        await settings.ReloadProxiesAsync();
        Assert.False(settings.HasValidationError);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal(proxy.Id, Assert.Single(repository.Providers).ProxyId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousProxyCompletionAfterCancellation_DoesNotPublishOrWrite(bool save)
    {
        var (repository, settings, proxy) = CreateSettings();
        using var owner = settings;
        var selected = settings.SelectedProxy;
        repository.ProxyLoader = () => Task.FromResult(CancellationObservedSnapshot.Read(
            repository.ProxyReadToken, new[] { proxy with { Name = "late" } }));

        await (save ? settings.SaveCommand.ExecuteAsync(null) : settings.ReloadProxiesAsync());

        Assert.True(repository.ProxyReadToken.IsCancellationRequested);
        Assert.Same(selected, settings.SelectedProxy);
        Assert.True(settings.HasValidationError);
        Assert.True(settings.IsFormOpen);
        Assert.Equal("draft", settings.Name);
        Assert.Empty(repository.Providers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_CancelsAndBoundsProxyReadsWithoutLateNotifications(bool save)
    {
        var (repository, settings, proxy) = CreateSettings();
        using var owner = settings;
        var delayed = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ProxyLoader = () => delayed.Task;
        var pending = save ? settings.SaveCommand.ExecuteAsync(null) : settings.ReloadProxiesAsync();
        var token = repository.ProxyReadToken;
        settings.Dispose();
        var notifications = 0;
        settings.PropertyChanged += (_, _) => notifications++;
        try
        {
            await pending.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.True(token.IsCancellationRequested);
        }
        finally
        {
            delayed.TrySetResult([proxy with { Name = "late" }]);
            await pending;
        }
        Assert.Equal(0, notifications);
        Assert.Equal("draft", settings.Name);
        Assert.Empty(repository.Providers);
    }

    private static (ProxyReloadProbeRepository Repository, ProviderSettingsViewModel Settings, ProxyProfile Proxy) CreateSettings()
    {
        var repository = new ProxyReloadProbeRepository();
        var proxy = new ProxyProfile(Guid.NewGuid(), "selected", ProxyProtocol.Http,
            "127.0.0.1", 8080, null, null, true, true);
        repository.Proxies.Add(proxy);
        var settings = new ProviderSettingsViewModel(repository, TimeSpan.FromMilliseconds(50));
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "draft";
        settings.ApiKey = "draft-fixture-key";
        return (repository, settings, proxy);
    }
}
