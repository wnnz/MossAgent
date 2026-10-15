using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxyEditorMutationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageFailure_KeepsDraftAndAllowsSafeRetry(bool delete)
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        viewModel.Name = "retained draft";
        repository.FailingOperation = delete ? "delete" : "save";
        var command = delete ? viewModel.DeleteCommand : viewModel.SaveCommand;

        Assert.Null(await Record.ExceptionAsync(() => command.ExecuteAsync(null)));
        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("retained draft", viewModel.Name);
        Assert.Contains("失败", viewModel.Status);
        Assert.DoesNotContain("fixture-secret", viewModel.Status);
        repository.FailingOperation = null;
        await command.ExecuteAsync(null);
        Assert.False(viewModel.IsFormOpen);
        if (delete) Assert.Empty(repository.Proxies);
        else Assert.Equal("retained draft", Assert.Single(repository.Proxies).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSave_DoesNotClearNewerOrReopenedDraft(bool reopen)
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = (_, token) => completion.Task.WaitAsync(token);
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "original save";
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        if (reopen) viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "new draft";
        completion.TrySetResult();
        await pending;

        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("new draft", viewModel.Name);
        Assert.Contains(repository.Proxies, proxy => proxy.Name == "original save");
        repository.BeforeWrite = null;
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Equal(reopen ? 3 : 2, repository.Proxies.Count);
        Assert.Contains(repository.Proxies, proxy => proxy.Name == "new draft");
    }

    [Fact]
    public async Task PendingSave_RejectsOtherWritesAndRefreshEvenWhenInvokedDirectly()
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWrite = (_, token) => completion.Task.WaitAsync(token);
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        viewModel.Name = "accepted write";
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        var rejectedSave = viewModel.SaveCommand.ExecuteAsync(null);
        var rejectedDelete = viewModel.DeleteCommand.ExecuteAsync(null);
        try
        {
            Assert.False(viewModel.DeleteCommand.CanExecute(null));
            Assert.False(viewModel.RefreshCommand.CanExecute(null));
            await viewModel.RefreshCommand.ExecuteAsync(null);
            Assert.Equal("accepted write", viewModel.Name);
            Assert.Single(repository.Writes);
        }
        finally
        {
            completion.TrySetResult();
            await Task.WhenAll(pending, rejectedSave, rejectedDelete);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovedTarget_IsNotRecreatedOrWritten(bool delete)
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        viewModel.Name = "retained draft";
        repository.Proxies.Clear();

        await (delete ? viewModel.DeleteCommand : viewModel.SaveCommand).ExecuteAsync(null);

        Assert.Empty(repository.Writes);
        Assert.Empty(repository.Proxies);
        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("retained draft", viewModel.Name);
        Assert.Contains("不可用", viewModel.Status);
    }

    [Fact]
    public async Task Refresh_UpdatesSavedReferenceWithoutOverwritingDirtyFields()
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        viewModel.OpenEditFormCommand.Execute(viewModel.Items[0]);
        viewModel.Name = "retained draft";
        viewModel.Port = 9001;
        repository.Proxies[0] = repository.Proxies[0] with { Port = 9090 };

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("retained draft", viewModel.Name);
        Assert.Equal(9001, viewModel.Port);
        Assert.Equal(9090, viewModel.SelectedItem!.Port);
    }

    [Fact]
    public async Task NotificationFailure_IsSafeAndRefreshDoesNotSaveAgain()
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "committed proxy";
        viewModel.ProxiesChanged += () => throw new IOException("fixture-secret-proxy-password");

        Assert.Null(await Record.ExceptionAsync(() => viewModel.SaveCommand.ExecuteAsync(null)));
        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("committed proxy", viewModel.Name);
        Assert.DoesNotContain("fixture-secret", viewModel.Status);
        Assert.Single(repository.Writes);
        Assert.Null(await Record.ExceptionAsync(() => viewModel.RefreshCommand.ExecuteAsync(null)));
        Assert.Single(repository.Writes);
        Assert.Equal(2, repository.Proxies.Count);
    }

    [Fact]
    public async Task ExplicitCancellation_ReachesStorageAndKeepsDraft()
    {
        var (repository, viewModel) = await CreateAsync();
        using var owner = viewModel;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storageToken = CancellationToken.None;
        repository.BeforeWrite = (_, token) => { storageToken = token; return completion.Task.WaitAsync(token); };
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "cancelled draft";
        var pending = viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.SaveCommand.Cancel();
        try { Assert.True(storageToken.IsCancellationRequested); }
        finally { completion.TrySetResult(); await pending; }

        Assert.Single(repository.Proxies);
        Assert.True(viewModel.IsFormOpen);
        Assert.Equal("cancelled draft", viewModel.Name);
    }

    private static async Task<(ProxyMutationProbeRepository Repository, ProxySettingsViewModel ViewModel)> CreateAsync()
    {
        var repository = new ProxyMutationProbeRepository();
        repository.Proxies.Add(new ProxyProfile(Guid.NewGuid(), "existing", ProxyProtocol.Http,
            "127.0.0.1", 7890, null, null, false, true));
        var viewModel = new ProxySettingsViewModel(repository, new RecordingProxyConnectionTester());
        await viewModel.RefreshCommand.ExecuteAsync(null);
        return (repository, viewModel);
    }
}
