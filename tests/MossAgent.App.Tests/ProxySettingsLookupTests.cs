using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxySettingsLookupTests
{
    [Fact]
    public async Task ExplicitTargets_ResolveCurrentCatalogRecordsWithoutChangingOtherDrafts()
    {
        var repository = Repository();
        using var settings = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        await settings.RefreshCommand.ExecuteAsync(null);
        var stale = repository.Proxies[0] with { Name = "stale name", IsEnabled = false };
        settings.OpenEditFormCommand.Execute(repository.Proxies[1]);
        settings.Name = "other draft";

        Assert.True(settings.OpenEditFormCommand.CanExecute(stale));
        Assert.True(settings.TestCommand.CanExecute(stale));
        Assert.Equal("other draft", settings.Name);
        settings.OpenEditFormCommand.Execute(stale);

        Assert.Same(repository.Proxies[0], settings.SelectedItem);
        Assert.Equal("first proxy", settings.Name);
        Assert.False(settings.IsEditorTargetUnavailable);
    }

    [Fact]
    public async Task MissingAndDisabledTargets_CannotBeTested()
    {
        var repository = Repository();
        using var settings = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        await settings.RefreshCommand.ExecuteAsync(null);
        Assert.False(settings.OpenEditFormCommand.CanExecute(null));
        Assert.False(settings.TestCommand.CanExecute(null));
        settings.OpenEditFormCommand.Execute(repository.Proxies[0]);
        Assert.True(settings.TestCommand.CanExecute(null));
        Assert.False(settings.TestCommand.CanExecute(repository.Proxies[1] with { IsEnabled = true }));
        var missing = repository.Proxies[0] with { Id = Guid.NewGuid() };
        Assert.False(settings.OpenEditFormCommand.CanExecute(missing));
        Assert.False(settings.TestCommand.CanExecute(missing));
        repository.Proxies.Clear();
        await settings.RefreshCommand.ExecuteAsync(null);

        Assert.True(settings.IsEditorTargetUnavailable);
        Assert.False(settings.OpenEditFormCommand.CanExecute(null));
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.DeleteCommand.CanExecute(null));
        Assert.False(settings.TestCommand.CanExecute(null));
    }

    [Fact]
    public async Task WarmCommandAndTargetChecks_DoNotAllocate()
    {
        var repository = Repository();
        using var settings = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        await settings.RefreshCommand.ExecuteAsync(null);
        var target = repository.Proxies[0];
        settings.OpenEditFormCommand.Execute(target);
        var valid = true;
        for (var index = 0; index < 100; index++) valid &= CheckCommands(settings, target);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++) valid &= CheckCommands(settings, target);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(valid);
        Assert.Equal(0, allocated);
    }

    private static bool CheckCommands(ProxySettingsViewModel settings, ProxyProfile target) =>
        settings.OpenEditFormCommand.CanExecute(target) && settings.TestCommand.CanExecute(target)
        && settings.SaveCommand.CanExecute(null) && settings.DeleteCommand.CanExecute(null)
        && !settings.IsEditorTargetUnavailable;

    private static ProxyMutationProbeRepository Repository()
    {
        var repository = new ProxyMutationProbeRepository();
        repository.Proxies.Add(new ProxyProfile(Guid.NewGuid(), "first proxy", ProxyProtocol.Http,
            "127.0.0.1", 8080, null, null, false, true));
        repository.Proxies.Add(new ProxyProfile(Guid.NewGuid(), "second proxy", ProxyProtocol.Socks5,
            "127.0.0.1", 1080, null, null, false, false));
        return repository;
    }
}
