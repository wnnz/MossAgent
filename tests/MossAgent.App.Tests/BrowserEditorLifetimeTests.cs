using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserEditorLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledOrDisposedRead_RejectsLateCompletion(bool dispose)
    {
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        var completion = new TaskCompletionSource<BrowserConfiguration>(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageToken = TestContext.Current.CancellationToken;
        repository.Loader = token => { storageToken = token; return completion.Task; };
        var pending = viewModel.RefreshCommand.ExecuteAsync(null);
        var before = viewModel.DefaultBrowser;

        if (dispose) viewModel.Dispose();
        else viewModel.RefreshCommand.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(storageToken.IsCancellationRequested);
        completion.TrySetResult(repository.Configuration with { DefaultBrowser = DefaultBrowserKind.Edge });
        Assert.Equal(before, viewModel.DefaultBrowser);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task DisposedSave_CancelsStorageAndDoesNotPublishLateSuccess()
    {
        var repository = new BrowserSettingsProbeRepository();
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageToken = TestContext.Current.CancellationToken;
        repository.BeforeWrite = token => { storageToken = token; return completion.Task; };
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.Dispose();
        var status = viewModel.Status;
        Assert.True(storageToken.IsCancellationRequested);
        completion.TrySetResult();
        await pending;

        Assert.Equal(DefaultBrowserKind.Embedded, repository.Configuration.DefaultBrowser);
        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        Assert.Equal(status, viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CooperativeWrite_CanBeCanceledOrBoundedByDeadline(bool cancelCommand)
    {
        var repository = new BrowserSettingsProbeRepository
        {
            BeforeWrite = token => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository(),
            cancelCommand ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(80));
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        if (cancelCommand) viewModel.SaveCommand.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(DefaultBrowserKind.Embedded, repository.Configuration.DefaultBrowser);
        Assert.Equal(DefaultBrowserKind.Chrome, viewModel.DefaultBrowser);
        Assert.True(viewModel.HasStatusError);
        Assert.Contains(cancelCommand ? "取消" : "超时", viewModel.Status);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task NonCooperativeWrite_RetainsOwnershipUntilStorageReturns()
    {
        var repository = new BrowserSettingsProbeRepository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = _ => completion.Task;
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository());
        viewModel.DefaultBrowser = DefaultBrowserKind.Chrome;
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.SaveCommand.Cancel();
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.RefreshCommand.CanExecute(null));
            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Single(repository.Writes);
        }
        finally { completion.TrySetResult(); await pending; }

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.HasStatusError);
        Assert.DoesNotContain("已保存", viewModel.Status);
    }

    [Fact]
    public async Task InitialReadDeadline_BlocksWritesAndUsesSafeError()
    {
        var repository = new BrowserSettingsProbeRepository
        {
            Loader = async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new IOException("synthetic-secret");
            }
        };
        using var viewModel = new BrowserSettingsViewModel(repository, new ConfigurationMutationProbeRepository(),
            TimeSpan.FromMilliseconds(80));
        await viewModel.Initialization.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.HasStatusError);
        Assert.True(viewModel.HasEditorNotice);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Contains("超时", viewModel.Status);
        Assert.DoesNotContain("synthetic-secret", viewModel.Status);
    }
}
