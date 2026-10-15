using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxyEditorLifetimeTests
{
    [Fact]
    public async Task DisposedEditor_CancelsWriteAndRejectsLatePublication()
    {
        var repository = Repository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageToken = TestContext.Current.CancellationToken;
        repository.BeforeWrite = (_, token) => { storageToken = token; return completion.Task; };
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "closing draft";
        var notifications = 0;
        viewModel.ProxiesChanged += () => { notifications++; return Task.CompletedTask; };
        var pending = viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.Dispose();
        Assert.True(storageToken.IsCancellationRequested);
        completion.TrySetResult();
        await pending;

        Assert.Single(repository.Proxies);
        Assert.Equal("closing draft", viewModel.Name);
        Assert.Equal(0, notifications);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
        Assert.False(viewModel.OpenCreateFormCommand.CanExecute(null));
    }

    [Fact]
    public async Task CooperativeDeadline_CancelsWriteAndKeepsDraft()
    {
        var repository = Repository();
        repository.BeforeWrite = (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token);
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester(), TimeSpan.FromMilliseconds(80));
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "timed draft";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.Proxies);
        Assert.True(viewModel.IsFormOpen);
        Assert.True(viewModel.HasStatusError);
        Assert.Contains("超时", viewModel.Status);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedRead_KeepsDraftAndBlocksWritesUntilRefreshRecovers()
    {
        var repository = Repository();
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        viewModel.Name = "retained draft";
        repository.Loader = _ => throw new IOException("fixture-secret-proxy-password");

        Assert.Null(await Record.ExceptionAsync(() => viewModel.RefreshCommand.ExecuteAsync(null)));
        Assert.True(viewModel.HasStatusError);
        Assert.True(viewModel.HasEditorNotice);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Writes);
        Assert.Equal("retained draft", viewModel.Name);
        Assert.DoesNotContain("fixture-secret", viewModel.Status);
        repository.Loader = null;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal("retained draft", viewModel.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledOrDisposedRefresh_RejectsLateCatalog(bool dispose)
    {
        var repository = Repository();
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        var completion = new TaskCompletionSource<IReadOnlyList<ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readToken = TestContext.Current.CancellationToken;
        repository.Loader = token => { readToken = token; return completion.Task; };
        var pending = viewModel.RefreshCommand.ExecuteAsync(null);

        if (dispose) viewModel.Dispose();
        else viewModel.RefreshCommand.Cancel();
        await pending;
        Assert.True(readToken.IsCancellationRequested);
        completion.TrySetResult([]);
        Assert.Single(viewModel.Items);
        Assert.False(viewModel.RefreshCommand.IsRunning);
    }

    [Fact]
    public async Task DisposedEditor_CancelsTesterWithoutPublishingItsLateResult()
    {
        var repository = Repository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var testToken = TestContext.Current.CancellationToken;
        var tester = new RecordingProxyConnectionTester
        {
            BeforeResult = token => { testToken = token; return completion.Task; }
        };
        using var viewModel = new ProxySettingsViewModel(repository, tester);
        var pending = viewModel.TestCommand.ExecuteAsync(viewModel.Items[0]);
        var status = viewModel.Status;

        viewModel.Dispose();
        await pending;
        Assert.True(testToken.IsCancellationRequested);
        completion.TrySetResult();
        Assert.Equal(status, viewModel.Status);
        Assert.False(viewModel.TestCommand.IsRunning);
    }

    [Fact]
    public async Task DeleteCompletion_DoesNotClearAnotherEditor()
    {
        var repository = Repository();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = (_, token) => completion.Task.WaitAsync(token);
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        var pending = viewModel.DeleteCommand.ExecuteAsync(null);
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "new draft";
        completion.TrySetResult();
        await pending;

        Assert.Empty(repository.Proxies);
        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("new draft", viewModel.Name);
        Assert.Null(viewModel.SelectedItem);
    }

    [Theory]
    [InlineData("", "127.0.0.1", 7890)]
    [InlineData("name", "", 7890)]
    [InlineData("name", "127.0.0.1", 0)]
    [InlineData("name", "invalid.example/path", 7890)]
    public async Task InvalidDraft_UsesSharedErrorStateWithoutStorageWrite(string name, string host, int port)
    {
        var repository = Repository();
        using var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = name;
        viewModel.Host = host;
        viewModel.Port = port;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsFormOpen);
        Assert.True(viewModel.HasStatusError);
        Assert.Empty(repository.Writes);
        Assert.NotEmpty(viewModel.Status);
    }

    private static ProxyMutationProbeRepository Repository()
    {
        var repository = new ProxyMutationProbeRepository();
        repository.Proxies.Add(new ProxyProfile(Guid.NewGuid(), "existing", ProxyProtocol.Http,
            "127.0.0.1", 7890, null, null, false, true));
        return repository;
    }
}
