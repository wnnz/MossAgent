using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserEditorNotificationTests
{
    [Fact]
    public async Task NotificationDuringSave_IsAppliedAfterTheOwnedWriteCompletes()
    {
        var configurations = new ConfigurationMutationProbeRepository();
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => completion.Task;
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        settings.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = settings.SaveCommand.ExecuteAsync(null);
        var proxyId = Guid.NewGuid();
        configurations.ProxyLoader = () => Task.FromResult(ProxySnapshot(proxyId));
        await settings.ReloadProxiesAsync();
        completion.TrySetResult();
        await pending;

        Assert.Contains(settings.Proxies, proxy => proxy.Id == proxyId);
        Assert.Equal(DefaultBrowserKind.Chrome, settings.DefaultBrowser);
        Assert.Null(settings.SelectedProxy!.Id);
        Assert.Single(repository.Writes);
    }

    [Fact]
    public async Task NotificationDuringRead_DoesNotPublishAnObsoleteProxySnapshotAsFinal()
    {
        var configurations = new ConfigurationMutationProbeRepository();
        using var settings = new BrowserSettingsViewModel(new BrowserSettingsProbeRepository(), configurations);
        var completion = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        configurations.ProxyLoader = () => completion.Task;
        var pending = settings.RefreshCommand.ExecuteAsync(null);
        var proxyId = Guid.NewGuid();
        await settings.ReloadProxiesAsync();
        configurations.ProxyLoader = () => Task.FromResult(ProxySnapshot(proxyId));
        completion.TrySetResult([]);
        await pending;

        Assert.Contains(settings.Proxies, proxy => proxy.Id == proxyId);
        Assert.Same(settings.Proxies.Single(proxy => proxy.Id is null), settings.SelectedProxy);
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    private static IReadOnlyList<ProxyProfile> ProxySnapshot(Guid id) =>
        [new(id, "synthetic refreshed proxy", ProxyProtocol.Http, "127.0.0.1", 7890, null, null, false, true)];

    [Fact]
    public async Task InitialRead_CoalescesNotificationsWithoutReplacingANewerDraft()
    {
        var stale = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var configurations = new ConfigurationMutationProbeRepository
        {
            ProxyLoader = () => { reads++; return stale.Task; }
        };
        using var settings = new BrowserSettingsViewModel(new BrowserSettingsProbeRepository(), configurations);
        settings.DefaultBrowser = DefaultBrowserKind.Edge;
        settings.ProfileDirectory = "newer-draft";
        await settings.ReloadProxiesAsync();
        await settings.ReloadProxiesAsync();
        await settings.ReloadProxiesAsync();
        var proxyId = Guid.NewGuid();
        configurations.ProxyLoader = () => { reads++; return Task.FromResult(ProxySnapshot(proxyId)); };
        stale.TrySetResult([]);
        await settings.Initialization;

        Assert.Equal(2, reads);
        Assert.Contains(settings.Proxies, proxy => proxy.Id == proxyId);
        Assert.Equal(DefaultBrowserKind.Edge, settings.DefaultBrowser);
        Assert.Equal("newer-draft", settings.ProfileDirectory);
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task NotificationDuringSaveValidation_RechecksAProxyRemovedFromThePendingSnapshot()
    {
        var proxyId = Guid.NewGuid();
        var configurations = new ConfigurationMutationProbeRepository
        {
            ProxyLoader = () => Task.FromResult(ProxySnapshot(proxyId))
        };
        var repository = new BrowserSettingsProbeRepository();
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        settings.SelectedProxy = settings.Proxies.Single(proxy => proxy.Id == proxyId);
        var stale = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        configurations.ProxyLoader = () => stale.Task;
        var pending = settings.SaveCommand.ExecuteAsync(null);
        await settings.ReloadProxiesAsync();
        configurations.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([]);
        stale.TrySetResult(ProxySnapshot(proxyId));
        await pending;

        Assert.Empty(repository.Writes);
        Assert.Equal(proxyId, settings.SelectedProxy?.Id);
        Assert.True(settings.IsProxyUnavailable);
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.Contains("不可用", settings.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_DiscardsQueuedRefreshAndRejectsLateReadOrWritePublication(bool saving)
    {
        var configurations = new ConfigurationMutationProbeRepository();
        var repository = new BrowserSettingsProbeRepository();
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => release.Task;
        repository.Loader = _ => release.Task.ContinueWith(_ => repository.Configuration,
            TaskScheduler.Default);
        if (saving) repository.Loader = null;
        var pending = saving ? settings.SaveCommand.ExecuteAsync(null) : settings.RefreshCommand.ExecuteAsync(null);
        await settings.ReloadProxiesAsync();
        var reads = repository.Reads;
        settings.Dispose();
        var status = settings.Status;
        configurations.ProxyLoader = () => Task.FromResult(ProxySnapshot(Guid.NewGuid()));
        release.TrySetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(reads, repository.Reads);
        Assert.Equal(status, settings.Status);
        Assert.Single(settings.Proxies);
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedQueuedRefresh_BlocksFurtherWritesWithoutUndoingTheCommittedSave()
    {
        var configurations = new ConfigurationMutationProbeRepository();
        var repository = new BrowserSettingsProbeRepository();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => release.Task;
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        settings.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = settings.SaveCommand.ExecuteAsync(null);
        configurations.ProxyLoader = () => throw new IOException("synthetic-secret-proxy-password");
        await settings.ReloadProxiesAsync();
        release.TrySetResult();
        await pending;

        Assert.Equal(DefaultBrowserKind.Chrome, repository.Configuration.DefaultBrowser);
        Assert.Single(repository.Writes);
        Assert.True(settings.HasStatusError);
        Assert.DoesNotContain("synthetic-secret", settings.Status);
        Assert.False(settings.SaveCommand.CanExecute(null));
        configurations.ProxyLoader = null;
        await settings.RefreshCommand.ExecuteAsync(null);
        Assert.True(settings.SaveCommand.CanExecute(null));
        Assert.Single(repository.Writes);
    }
}
