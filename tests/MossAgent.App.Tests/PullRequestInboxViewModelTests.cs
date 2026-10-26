using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestInboxViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static SourceAccount Account(bool enabled = true) =>
        new(Guid.NewGuid(), "inbox account", new Uri("https://api.github.com/"), "synthetic-pr-credential", null, enabled);

    private static PullRequestInboxPage Page(int page, int items, bool next, bool incomplete = false, long total = 21) =>
        new(Enumerable.Range(1, items).Select(index => new PullRequestInboxItem(index, index,
            $"PR {index}", "octocat", "fixture", "repo", "open", false, null)).ToArray(), page, next, total, incomplete);

    [Fact]
    public Task LoadRequiresAccountAndPopulatesItemsPageLabelAndStatus() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe { RespondAsync = (_, _, page, _) => Task.FromResult(Page(page, 3, false, total: 3)) };
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        Assert.False(model.LoadCommand.CanExecute(null));
        Assert.False(model.IsEmpty);
        model.SetAccount(new SourceAccountItemViewModel(accounts.Store(Account())));
        Assert.True(model.LoadCommand.CanExecute(null));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(3, model.Items.Count);
        Assert.Equal(1, model.Page);
        Assert.Equal(PullRequestText.Get("Loaded"), model.Status);
        Assert.True(model.HasStatus);
        Assert.True(model.IsLoaded);
        Assert.False(model.NextCommand.CanExecute(null));
        Assert.False(model.IsIncomplete);
        Assert.Equal((PullRequestInboxFilter.Involves, 1), Assert.Single(client.Calls));
        Assert.Equal(2, accounts.GetCalls);
    }, TestContext.Current.CancellationToken);

    private static readonly (PullRequestInboxFilter, int)[] InvolvesThenReviewRequested =
        [(PullRequestInboxFilter.Involves, 1), (PullRequestInboxFilter.Involves, 2), (PullRequestInboxFilter.ReviewRequested, 1)];

    [Fact]
    public Task FilterSelectionIssuesNewRequestAndPagingFollowsResultFlags() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe();
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        model.SetAccount(new SourceAccountItemViewModel(accounts.Store(Account())));
        client.RespondAsync = (_, _, page, _) => Task.FromResult(Page(page, 20, next: page == 1));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(20, model.Items.Count);
        Assert.True(model.NextCommand.CanExecute(null));
        Assert.False(model.PreviousCommand.CanExecute(null));
        await model.NextCommand.ExecuteAsync(null);
        Assert.Equal(2, model.Page);
        Assert.False(model.NextCommand.CanExecute(null));
        Assert.True(model.PreviousCommand.CanExecute(null));
        model.SelectedFilter = model.Filters.Single(item => item.Filter == PullRequestInboxFilter.ReviewRequested);
        if (model.LoadCommand.ExecutionTask is { } work) await work;
        Assert.Equal(InvolvesThenReviewRequested, client.Calls);
        Assert.Equal(1, model.Page);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task StalePageIsDroppedWhenClearAdvancesGeneration() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe();
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        model.SetAccount(new SourceAccountItemViewModel(accounts.Store(Account())));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RespondAsync = async (_, _, page, _) => { entered.TrySetResult(); await gate.Task; return Page(page, 1, false); };
        var running = model.LoadCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(model.IsBusy);
        model.Clear();
        gate.TrySetResult();
        await running;
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
        Assert.Equal(string.Empty, model.Status);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Single(model.Items);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisabledOrRemovedAccountSurfacesDeniedStatus() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe();
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        model.SetAccount(new SourceAccountItemViewModel(accounts.Store(Account(enabled: false))));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("ScopeDenied"), model.Status);
        Assert.Empty(client.Calls);
        var stored = accounts.Store(Account());
        model.SetAccount(new SourceAccountItemViewModel(stored));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("Loaded"), model.Status);
        Assert.Single(model.Items);
        await accounts.DeleteAsync(stored.Id, stored.Revision, TestContext.Current.CancellationToken);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("ScopeDenied"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task IncompletePageSurfacesStatusAndRevisionChangeIsDenied() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe();
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        var stored = accounts.Store(Account());
        model.SetAccount(new SourceAccountItemViewModel(stored));
        client.RespondAsync = (_, _, page, _) => Task.FromResult(Page(page, 1, false, incomplete: true));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.True(model.IsIncomplete);
        Assert.Equal(PullRequestText.Get("InboxIncomplete"), model.Status);
        client.RespondAsync = async (account, _, page, token) =>
        {
            await accounts.SaveAsync(stored, token);
            return Page(page, 1, false);
        };
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("ScopeDenied"), model.Status);
        Assert.Empty(model.Items);
        Assert.False(model.IsIncomplete);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearResetsStateAndDisposeBlocksFurtherReads() => _session.DispatchAsync(async () =>
    {
        var accounts = new SourceAccountStoreProbe();
        var client = new PullRequestInboxClientProbe();
        var model = new PullRequestInboxViewModel(new SourceInboxReader(accounts, client));
        model.SetAccount(new SourceAccountItemViewModel(accounts.Store(Account())));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Single(model.Items);
        model.Clear();
        Assert.Empty(model.Items);
        Assert.Equal(string.Empty, model.Status);
        await model.DisposeAsync();
        Assert.False(model.LoadCommand.CanExecute(null));
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Single(client.Calls);
    }, TestContext.Current.CancellationToken);
}
