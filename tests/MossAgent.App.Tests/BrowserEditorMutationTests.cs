using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserEditorMutationTests
{
    [Fact]
    public async Task Refresh_PreservesUnsavedBrowserAndProfileDraft()
    {
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        viewModel.ProfileDirectory = "synthetic-draft-directory";

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        Assert.Equal("synthetic-draft-directory", viewModel.ProfileDirectory);
    }

    [Fact]
    public async Task PendingInitialRead_BlocksWritesAndPreservesNewDraft()
    {
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource<BrowserConfiguration>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Loader = _ => completion.Task;
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        try
        {
            viewModel.DefaultBrowser = DefaultBrowserKind.Edge;
            Assert.False(viewModel.SaveCommand.CanExecute(null));
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Empty(repository.Writes);
        }
        finally { completion.TrySetResult(repository.Configuration); }
        await viewModel.Initialization;
        Assert.Equal(DefaultBrowserKind.Edge, viewModel.DefaultBrowser);
    }

    [Fact]
    public async Task FailedRead_BlocksWritesUntilRefreshRecoversWithoutLosingDraft()
    {
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        repository.Loader = _ => throw new IOException("synthetic-secret-browser-key");

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Writes);
        Assert.DoesNotContain("synthetic-secret", viewModel.Status);
        repository.Loader = null;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
    }

    [Fact]
    public async Task UnavailableSavedProxy_IsRetainedRatherThanSilentlyBecomingDirect()
    {
        var proxyId = Guid.NewGuid();
        var repository = new BrowserSettingsProbeRepository
        {
            Configuration = new(DefaultBrowserKind.Chrome, BrowserProfilePreference.Managed, null, null, proxyId)
        };
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());

        Assert.Equal(proxyId, viewModel.SelectedProxy?.Id);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Writes);
        viewModel.SelectedProxy = viewModel.Proxies.Single(proxy => proxy.Id is null);
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Null(Assert.Single(repository.Writes).ProxyId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSave_RejectsDirectReentryAndRefresh(bool synchronous)
    {
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => completion.Task;
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        try
        {
            Assert.False(viewModel.RefreshCommand.CanExecute(null));
            if (synchronous) viewModel.SaveCommand.Execute(null);
            else await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Single(repository.Writes);
        }
        finally { completion.TrySetResult(); await pending; }
    }

    [Fact]
    public async Task SaveCompletion_PreservesNewDraftAndReportsEarlierSnapshot()
    {
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => completion.Task;
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.DefaultBrowser = DefaultBrowserKind.Edge;
        completion.TrySetResult();
        await pending;

        Assert.Equal(DefaultBrowserKind.Chrome, repository.Configuration.DefaultBrowser);
        Assert.Equal(DefaultBrowserKind.Edge, viewModel.DefaultBrowser);
        Assert.Contains("上一版", viewModel.Status);
    }

    [Fact]
    public async Task Save_RevalidatesProxyThatDisappearsAfterLoading()
    {
        var proxyId = Guid.NewGuid();
        var configurations = new ConfigurationMutationProbeRepository
        {
            ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>(
                [new(proxyId, "synthetic-proxy", ProxyProtocol.Http, "127.0.0.1", 7890, null, null, false, true)])
        };
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, configurations);
        viewModel.SelectedProxy = viewModel.Proxies.Single(proxy => proxy.Id == proxyId);
        configurations.ProxyLoader = () => Task.FromResult<IReadOnlyList<ProxyProfile>>([]);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(repository.Writes);
        Assert.Equal(proxyId, viewModel.SelectedProxy?.Id);
        Assert.Contains("不可用", viewModel.Status);
    }
}
