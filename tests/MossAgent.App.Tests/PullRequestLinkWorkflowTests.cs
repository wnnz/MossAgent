using System.Net;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestLinkWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task KeyboardLinkOpeningUsesOnlyAnAuthorizedDetailRequestAndRetainsGrantDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        model.Owner = "unsaved-owner"; model.Repository = "unsaved-repository";
        var editorAccount = model.SelectedAccount;
        var requests = new List<Uri>();
        fixture.Transport.RespondAsync = (request, _) =>
        {
            Assert.False(Avalonia.Threading.Dispatcher.UIThread.CheckAccess());
            Assert.Equal(HttpMethod.Get, request.Method);
            requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal)
                ? $"[{PullRequestTestData.File()}]" : PullRequestTestData.Item(42).Replace("\"state\":\"open\"", "\"state\":\"closed\"", StringComparison.Ordinal)) });
        };
        var view = new PullRequestsView { DataContext = model };
        var window = new Window { Content = view, Width = 980, Height = 740 };
        window.Show();
        try
        {
            window.UpdateLayout(); Press(view, model.Link.ShowCommand); window.UpdateLayout();
            Assert.Null(model.Link.Account); Assert.False(model.Link.OpenCommand.CanExecute(null));
            var dialog = Assert.Single(view.GetVisualDescendants().OfType<AppSettingsDialog>(), control => ReferenceEquals(control.DataContext, model.Link));
            var picker = Assert.Single(dialog.GetVisualDescendants().OfType<AppComboBox>());
            picker.SelectedItem = Assert.Single(model.Accounts);
            var input = Assert.Single(dialog.GetVisualDescendants().OfType<AppInput>());
            Assert.Equal(PullRequestText.LinkUrl, AutomationProperties.GetName(input));
            Assert.True(input.Focus()); window.KeyTextInput("https://github.com/Fixture/Repo/pull/42/files?w=1#discussion_r7");
            Assert.Equal(0, fixture.Transport.RequestCount);
            Press(dialog, model.Link.OpenCommand); await model.Link.OpenCommand.ExecutionTask!; window.UpdateLayout();
            Assert.False(model.Link.IsOpen, model.Link.Status); Assert.False(model.IsEmpty);
            Assert.Equal(42, model.Detail?.Number); Assert.Equal("closed", model.Detail?.State);
            Assert.Equal(model.Detail, Assert.Single(model.Items)); Assert.True(model.Inspection.IsActive);
            Assert.Equal("https://api.github.com/repos/fixture/repo/pulls/42", Assert.Single(requests).AbsoluteUri);
            Assert.Contains(view.GetVisualDescendants().OfType<AppSelectableText>(), text => text.IsEffectivelyVisible && text.Text == "Untrusted PR description");
            Assert.Equal("unsaved-owner", model.Owner); Assert.Equal("unsaved-repository", model.Repository); Assert.Same(editorAccount, model.SelectedAccount);
            var taskId = await fixture.ReviewSessions.GetOrCreateAsync(model.SelectedRepository!.Scope, fixture.Token);
            var record = Assert.Single(await fixture.Audit.GetRecentAsync(taskId, 10, fixture.Token));
            Assert.Equal("source.pr_get", record.ToolName); Assert.True(record.IsSuccess); Assert.NotNull(record.CompletedAt);
            await model.Inspection.Files.LoadCommand.ExecuteAsync(null);
            Assert.Single(model.Inspection.Files.Items); Assert.True(model.Chat!.ReadOnlyCommand.CanExecute(null));
            Assert.All(requests, uri => { Assert.Equal("api.github.com", uri.Host); Assert.DoesNotContain("discussion", uri.AbsoluteUri, StringComparison.Ordinal); });
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EnterpriseLinkUsesTheConfiguredApiPrefixPortAndChosenAccount() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync("https://enterprise.example.test:8443/api/v3/");
        var model = fixture.Main.Pages.PullRequests!;
        fixture.Transport.RespondAsync = (request, _) =>
        {
            Assert.Equal("https://enterprise.example.test:8443/api/v3/repos/fixture/repo/pulls/7", request.RequestUri!.AbsoluteUri);
            Assert.Equal("synthetic-link-credential", request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) });
        };
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
        model.Link.Url = "https://enterprise.example.test:8443/fixture/repo/pull/7/commits/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa#details";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(model.Link.IsOpen, model.Link.Status); Assert.Equal(7, model.Detail?.Number);
        Assert.Equal(1, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    private static void Press(Control view, System.Windows.Input.ICommand command)
    {
        var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == command && button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled); Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
