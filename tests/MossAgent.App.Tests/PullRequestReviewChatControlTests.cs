using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestReviewChatControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 740)]
    [InlineData(true, 980, 740)]
    public Task KeyboardInvestigationSeedsCachedEmptyChatAndPreservesOrdinaryDraft(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        fixture.Workspace.ComposerText = "ordinary foreground draft";
        await fixture.Workspace.Drafts.FlushAsync();
        var task = await fixture.OpenAsync();
        Assert.Empty(fixture.Workspace.ComposerText);
        await fixture.ReturnToPullRequestAsync();
        await LoadFailureAsync(fixture);
        var sourceRequests = fixture.Source.Transport.RequestCount;
        var view = new PullRequestsView { DataContext = fixture.Page };
        var window = new Window { Content = view, Width = width, Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == fixture.Chat.InvestigateCommand);
            Assert.True(button.IsEffectivelyEnabled);
            button.BringIntoView(); window.UpdateLayout();
            Assert.True(button.Focus(NavigationMethod.Tab));
            var bounds = new Rect(button.TranslatePoint(default, window)!.Value, button.Bounds.Size);
            Assert.True(new Rect(window.ClientSize).Contains(bounds), $"Investigation button outside viewport: {bounds}");
            Capture(window, $"pr-review-chat-{dark}-{width}-{height}");

            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            await fixture.Chat.InvestigateCommand.ExecutionTask!;
            await fixture.WaitUntilAsync(() => fixture.Source.Main.Pages.IsConversationOpen);
            await fixture.Workspace.Drafts.LoadCurrentAsync();

            Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask?.Id);
            Assert.Contains("Build", fixture.Workspace.ComposerText, StringComparison.Ordinal);
            Assert.Contains("failure", fixture.Workspace.ComposerText, StringComparison.Ordinal);
            Assert.Equal(fixture.Workspace.ComposerText, (await fixture.Drafts.GetAsync(new(null, task.Id), fixture.Token))?.Text);
            Assert.Equal("ordinary foreground draft", (await fixture.Drafts.GetAsync(new(null, null), fixture.Token))?.Text);
            Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
            Assert.Empty(fixture.Transport.Requests);
            Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
            await fixture.WaitUntilAsync(() => fixture.Workspace.ModelPicker.CanSendWithSelectedModel);
            Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvestigatingAgainKeepsUnsentUserEditsAndNeverAutoSends() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        fixture.Workspace.ComposerText = "my local unsent review";
        await fixture.ReturnToPullRequestAsync();
        await LoadFailureAsync(fixture);

        var reopened = await fixture.OpenAsync(investigate: true);

        Assert.Equal(task.Id, reopened.Id);
        Assert.Equal("my local unsent review", fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Transport.Requests);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SuccessfulOrUnloadedChecksCannotPrepareAnInvestigation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        Assert.False(fixture.Chat.InvestigateCommand.CanExecute(null));
        await fixture.Page.Inspection.Runs.LoadCommand.ExecuteAsync(null);
        Assert.False(fixture.Chat.InvestigateCommand.CanExecute(null));
        await fixture.Chat.InvestigateCommand.ExecuteAsync(null);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
        Assert.Empty(fixture.Transport.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpenButtonUsesKeyboardAndNeverSendsAModelRequest() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var view = new PullRequestsView { DataContext = fixture.Page };
        var window = new Window { Content = view, Width = 980, Height = 740 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == fixture.Chat.OpenCommand);
            button.BringIntoView(); window.UpdateLayout();
            Assert.True(button.Focus(NavigationMethod.Tab));
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            await fixture.Chat.OpenCommand.ExecutionTask!;
            await fixture.WaitUntilAsync(() => fixture.Source.Main.Pages.IsConversationOpen);
            Assert.NotNull(fixture.Workspace.Catalog.SelectedTask);
            Assert.Empty(fixture.Transport.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NavigationDuringDelayedDraftPreparationRejectsLateConversationNavigation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await LoadFailureAsync(fixture);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Chat.PrepareDraft = async (_, _, _) => { entered.TrySetResult(); await release.Task; return true; };
        var opening = fixture.Chat.InvestigateCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);

        fixture.Source.Main.Pages.IsPullRequestsOpen = false;
        release.SetResult();
        await opening;

        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.Empty(fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Transport.Requests);
        Assert.False(fixture.Chat.OpenCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    private static async Task LoadFailureAsync(PullRequestReviewChatFixture fixture)
    {
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var response = await PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);
            response.Content.Dispose();
            response.Content = new StringContent(body.Replace("success", "failure", StringComparison.Ordinal));
            return response;
        };
        await fixture.Page.Inspection.Runs.LoadCommand.ExecuteAsync(null);
        Assert.True(fixture.Chat.InvestigateCommand.CanExecute(null));
    }

    private static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
