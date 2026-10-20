using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Domain;
using System.Net;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task AuthorizationToPipelineHttpVisibleDetailsAndRevocation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection", new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        var requests = new List<Uri>();
        fixture.Transport.RespondAsync = (request, _) =>
        {
            Assert.False(Avalonia.Threading.Dispatcher.UIThread.CheckAccess());
            Assert.Equal("synthetic-pr-credential", request.Headers.Authorization!.Parameter);
            requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(request.RequestUri!.Query.Length > 0 ? $"[{PullRequestTestData.Item()}]" : PullRequestTestData.Item()) });
        };
        var model = fixture.Main.Pages.PullRequests!;
        fixture.Main.Pages.OpenPullRequestsCommand.Execute(null);
        await model.RefreshCommand.ExecutionTask!;
        Assert.True(fixture.Main.Pages.IsPullRequestsOpen);
        Assert.False(fixture.Main.Pages.IsConversationOpen);
        var view = new PullRequestsView { DataContext = model };
        var window = new Window { Content = view, Width = 980, Height = 640 };
        window.Show();
        try
        {
            model.CreateCommand.Execute(null);
            model.Owner = "fixture"; model.Repository = "repo";
            await model.SaveGrantCommand.ExecuteAsync(null);
            Assert.False(model.IsEditorOpen, model.Status);
            Assert.NotNull(model.SelectedRepository);
            window.UpdateLayout();
            Press(view, model.LoadCommand);
            await model.LoadCommand.ExecutionTask!;
            Assert.Single(model.Items);
            model.SelectedItem = model.Items[0];
            Press(view, model.DetailCommand);
            await model.DetailCommand.ExecutionTask!;
            Assert.Equal("Untrusted PR description", model.Detail?.Body);
            window.UpdateLayout();
            Assert.Contains(view.GetVisualDescendants().OfType<AppSelectableText>(), text => text.Text == "Untrusted PR description" && text.IsEffectivelyVisible);
            Assert.Equal("https://api.github.com/repos/fixture/repo/pulls?state=open&sort=updated&direction=desc&per_page=20&page=1", requests[0].AbsoluteUri);
            Assert.Equal("https://api.github.com/repos/fixture/repo/pulls/7", requests[1].AbsoluteUri);
            var auditTask = await fixture.ReviewSessions.GetOrCreateAsync(model.SelectedRepository!.Scope, fixture.Token);
            var records = await fixture.Audit.GetRecentAsync(auditTask, 10, fixture.Token);
            Assert.Equal(2, records.Count);
            Assert.All(records, record => { Assert.True(record.IsSuccess); Assert.NotNull(record.CompletedAt); });
            await model.RevokeCommand.ExecuteAsync(null);
            Assert.Empty(model.Repositories);
            Assert.Empty(model.Items);
            Assert.Null(model.Detail);
            Assert.False(model.LoadCommand.CanExecute(null));
            Assert.Equal(2, fixture.Transport.DisposedHandlers);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NavigationCancelsReadAndDoesNotPublishIntoAnotherPage() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection", new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        model.CreateCommand.Execute(null); model.Owner = "fixture"; model.Repository = "repo";
        await model.SaveGrantCommand.ExecuteAsync(null);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.RespondAsync = async (_, token) =>
        { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); };
        var running = model.LoadCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(fixture.Token);
        model.Leave();
        await running;
        Assert.Empty(model.Items);
        Assert.Null(model.Detail);
        Assert.False(model.IsBusy);
        Assert.Equal(1, fixture.Transport.DisposedHandlers);
        Assert.True(model.LoadCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PaginationUsesLocalPageNumbersAndSettingsClosesTheSourcePage() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection", new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        model.CreateCommand.Execute(null); model.Owner = "fixture"; model.Repository = "repo";
        await model.SaveGrantCommand.ExecuteAsync(null);
        var queries = new List<string>();
        fixture.Transport.RespondAsync = (request, _) =>
        {
            var query = request.RequestUri!.Query; queries.Add(query);
            var items = query.EndsWith("page=1", StringComparison.Ordinal)
                ? "[" + string.Join(",", Enumerable.Range(1, 20).Select(number => PullRequestTestData.Item(number))) + "]" : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(items) });
        };
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(20, model.Items.Count);
        Assert.True(model.NextCommand.CanExecute(null));
        await model.NextCommand.ExecuteAsync(null);
        Assert.Empty(model.Items);
        Assert.Equal(2, model.Page);
        Assert.False(model.NextCommand.CanExecute(null));
        Assert.True(model.PreviousCommand.CanExecute(null));
        await model.PreviousCommand.ExecuteAsync(null);
        Assert.Equal(1, model.Page);
        Assert.Equal(20, model.Items.Count);
        Assert.EndsWith("page=2", queries[1], StringComparison.Ordinal);
        fixture.Main.Pages.IsPullRequestsOpen = true;
        fixture.Main.OpenSettingsCommand.Execute(null);
        Assert.False(fixture.Main.Pages.IsPullRequestsOpen);
        Assert.Empty(model.Items);
        Assert.Null(model.Detail);
    }, TestContext.Current.CancellationToken);

    private static void Press(Control view, System.Windows.Input.ICommand command)
    {
        var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == command);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    [Fact]
    public Task ProductionNavigationRailOpensTheActualPrPageWithoutLocalGrant() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var main = fixture.Main;
        var window = new MainWindow { DataContext = main, Width = 980, Height = 640 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
                button => button.IsEffectivelyVisible && button.Command == main.Pages.OpenPullRequestsCommand);
            Assert.True(button.Focus(NavigationMethod.Tab));
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            await main.Pages.PullRequests!.RefreshCommand.ExecutionTask!;
            window.UpdateLayout();
            Assert.True(main.Pages.IsPullRequestsOpen);
            Assert.False(main.Pages.IsConversationOpen);
            Assert.Contains(window.GetVisualDescendants().OfType<PullRequestsView>(), view => view.IsEffectivelyVisible);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<WorkspaceSidebar>(), view => view.IsEffectivelyVisible);
            Assert.Empty(main.Pages.PullRequests.Repositories);
            Assert.False(main.Pages.PullRequests.LoadCommand.CanExecute(null));
            Assert.Equal(0, fixture.Transport.RequestCount);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
