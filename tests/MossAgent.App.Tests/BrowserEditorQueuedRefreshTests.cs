using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserEditorQueuedRefreshTests
{
    [Fact]
    public async Task NotificationsDuringTheDrain_AreCoalescedIntoTheNextSnapshot()
    {
        var configurations = new ConfigurationMutationProbeRepository();
        var repository = new BrowserSettingsProbeRepository();
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        var first = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        configurations.ProxyLoader = () => first.Task;
        var pending = settings.RefreshCommand.ExecuteAsync(null);
        await settings.ReloadProxiesAsync();
        configurations.ProxyLoader = () => { started.TrySetResult(); return queued.Task; };
        first.TrySetResult([]);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.RefreshCommand.CanExecute(null));
        Assert.True(settings.HasEditorNotice);
        await settings.ReloadProxiesAsync();
        await settings.ReloadProxiesAsync();
        var latestId = Guid.NewGuid();
        configurations.ProxyLoader = () => Task.FromResult(Snapshot(latestId));
        var obsoleteId = Guid.NewGuid();
        queued.TrySetResult(Snapshot(obsoleteId));
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(4, repository.Reads);
        Assert.Contains(settings.Proxies, proxy => proxy.Id == latestId);
        Assert.DoesNotContain(settings.Proxies, proxy => proxy.Id == obsoleteId);
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task CanceledNoncooperativeWrite_DrainsNotificationsOnlyAfterStorageReturns()
    {
        var configurations = new ConfigurationMutationProbeRepository();
        var repository = new BrowserSettingsProbeRepository();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => release.Task;
        using var settings = new BrowserSettingsViewModel(repository, configurations);
        settings.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = settings.SaveCommand.ExecuteAsync(null);
        var reads = repository.Reads;
        var proxyId = Guid.NewGuid();
        configurations.ProxyLoader = () => Task.FromResult(Snapshot(proxyId));
        await settings.ReloadProxiesAsync();
        settings.SaveCommand.Cancel();
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.True(settings.IsBusy);
            Assert.False(settings.SaveCommand.CanExecute(null));
            await settings.SaveCommand.ExecuteAsync(null);
            Assert.Equal(reads, repository.Reads);
            Assert.Single(repository.Writes);
        }
        finally { release.TrySetResult(); await pending; }

        Assert.Contains(settings.Proxies, proxy => proxy.Id == proxyId);
        Assert.Equal(DefaultBrowserKind.Embedded, repository.Configuration.DefaultBrowser);
        Assert.Equal(DefaultBrowserKind.Chrome, settings.DefaultBrowser);
        Assert.Contains("取消", settings.Status);
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    private static IReadOnlyList<ProxyProfile> Snapshot(Guid id) =>
        [new(id, "synthetic refreshed proxy", ProxyProtocol.Http, "127.0.0.1", 7890, null, null, false, true)];
}
