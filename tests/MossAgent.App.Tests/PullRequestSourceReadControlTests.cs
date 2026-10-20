using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestSourceReadControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 740)]
    [InlineData(true, 980, 740)]
    public Task KeyboardSourceReadEntryIsVisibleLabeledAndDoesNotSend(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var sourceRequests = fixture.Source.Transport.RequestCount;
        var view = new PullRequestsView { DataContext = fixture.Page };
        var window = new Window { Content = view, Width = width, Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == fixture.Chat.ReadOnlyCommand);
            Assert.True(button.IsEffectivelyEnabled);
            Assert.Equal(PullRequestText.OpenReadOnlyChat, AutomationProperties.GetName(button));
            Assert.Equal(PullRequestText.ReadOnlyChatHint, AutomationProperties.GetHelpText(button));
            button.BringIntoView(); window.UpdateLayout();
            Assert.True(button.Focus(NavigationMethod.Tab));
            var bounds = new Rect(button.TranslatePoint(default, window)!.Value, button.Bounds.Size);
            Assert.True(new Rect(window.ClientSize).Contains(bounds), $"Source read button outside viewport: {bounds}");
            var viewer = button.FindAncestorOfType<AppScrollViewer>()!;
            var disclosure = Assert.Single(viewer.GetVisualDescendants().OfType<AppText>(), text => text.Text == PullRequestText.ReadOnlyChatHint);
            var scrollbar = Assert.Single(viewer.GetVisualDescendants().OfType<ScrollBar>(),
                bar => bar.Orientation == Avalonia.Layout.Orientation.Vertical && ReferenceEquals(bar.TemplatedParent, viewer));
            var rightEdge = scrollbar.IsEffectivelyVisible && scrollbar.Bounds.Width > 0
                ? scrollbar.TranslatePoint(default, window)!.Value.X : viewer.TranslatePoint(new Point(viewer.Bounds.Width, 0), window)!.Value.X;
            Capture(window, $"pr-source-read-{dark}-{width}-{height}");
            Assert.True(disclosure.TranslatePoint(default, window)!.Value.X + disclosure.Bounds.Width
                <= rightEdge,
                $"Permission disclosure must not be covered by the scrollbar: text={disclosure.Bounds}, textOrigin={disclosure.TranslatePoint(default, window)}, scrollbar={scrollbar.Bounds}, scrollbarOrigin={scrollbar.TranslatePoint(default, window)}, viewport={viewer.Viewport}, padding={viewer.Padding}.");

            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            await fixture.Chat.ReadOnlyCommand.ExecutionTask!;
            await fixture.WaitUntilAsync(() => fixture.Source.Main.Pages.IsConversationOpen && fixture.Workspace.Catalog.SelectedTask is not null);

            var task = fixture.Workspace.Catalog.SelectedTask!;
            Assert.Null(task.ProjectId); Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
            Assert.Equal(SourceReviewChatMode.ReadOnlySource, (await fixture.Chats.GetAsync(task.Id, fixture.Token))!.Mode);
            Assert.Empty(fixture.Transport.Requests); Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
            Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
