using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ConversationTimelineLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, "Light")]
    [InlineData(740, "Light")]
    [InlineData(280, "Dark")]
    [InlineData(740, "Dark")]
    public Task Summary_UsesSharedGeometryAndKeyboardDisclosureWithoutHidingTheReply(double width, string theme) =>
        _session.Dispatch(() =>
        {
            var now = DateTimeOffset.UtcNow;
            var messages = new ObservableCollection<ChatMessageViewModel>
            {
                new("你", "inspect", createdAt: now),
                ChatMessageViewModel.CreateTool(new("subagent.run", true, "child complete", null,
                    [new ToolDialogueMessage("子代理", "## child reply") ])),
                new("MossAgent", "# final reply", createdAt: now.AddSeconds(45))
            };
            var timeline = new AppConversationTimeline { Messages = messages };
            var window = new Window
            {
                Width = width,
                Height = 640,
                Content = timeline,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try { VerifyDisclosure(window, timeline, width); }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void VerifyDisclosure(Window window, AppConversationTimeline timeline, double width)
    {
        window.UpdateLayout();
        var turn = Assert.Single(timeline.Timeline.Turns);
        var summary = Assert.Single(timeline.GetVisualDescendants().OfType<AppActionButton>(),
            button => button.Classes.Contains("conversation-turn-summary"));
        Assert.Equal(32, summary.Bounds.Height);
        Assert.InRange(summary.Bounds.Width, 1, width);
        Assert.True(summary.IsEffectivelyVisible);
        Assert.False(turn.IsExpanded);
        var chevron = Assert.Single(summary.GetVisualDescendants().OfType<AppIcon>(),
            icon => icon.Classes.Contains("action-trailing-icon"));
        Assert.Equal(12, chevron.Bounds.Width);
        var timestamp = Assert.Single(timeline.GetVisualDescendants().OfType<AppText>(),
            text => text.Classes.Contains("conversation-turn-timestamp"));
        Assert.Equal(13, timestamp.FontSize);
        Assert.True(timestamp.IsEffectivelyVisible);
        var response = Assert.Single(timeline.GetVisualDescendants().OfType<AppResponseContent>(),
            view => ReferenceEquals(view.DataContext, turn.Response));
        Assert.True(response.IsEffectivelyVisible);
        summary.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.UpdateLayout();
        Assert.True(turn.IsExpanded);
        var toolRow = Assert.Single(timeline.GetVisualDescendants().OfType<AppToolActivityRow>(),
            row => ReferenceEquals(row.DataContext, Assert.Single(turn.Activities)));
        Assert.True(toolRow.IsEffectivelyVisible);
        Assert.InRange(toolRow.Bounds.Width, 1, width);
        Assert.True(response.IsEffectivelyVisible);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.UpdateLayout();
        Assert.False(turn.IsExpanded);
        Assert.False(toolRow.IsEffectivelyVisible);
        Assert.True(response.IsEffectivelyVisible);
    }

    [Fact]
    public Task DetachAndReattach_RebindsOnlyTheCurrentSource() => _session.Dispatch(() =>
    {
        var old = new ObservableCollection<ChatMessageViewModel> { new("你", "old") };
        var current = new ObservableCollection<ChatMessageViewModel> { new("你", "current") };
        var timeline = new AppConversationTimeline { Messages = old };
        var window = new Window { Content = timeline };
        window.Show();
        try
        {
            Assert.Equal("old", Assert.Single(timeline.Timeline.Turns).UserMessage!.Content);
            window.Content = null;
            Assert.Empty(timeline.Timeline.Turns);
            timeline.Messages = current;
            old.Add(new("MossAgent", "late"));
            window.Content = timeline;
            window.UpdateLayout();
            Assert.Equal("current", Assert.Single(timeline.Timeline.Turns).UserMessage!.Content);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ACancelledToolOnlyTurn_DoesNotRenderAnEmptyResponseOrGhostControls(bool hasUser) => _session.Dispatch(() =>
    {
        var messages = new ObservableCollection<ChatMessageViewModel>();
        if (hasUser) messages.Add(new("你", "cancelled prompt"));
        messages.Add(ChatMessageViewModel.CreateTool(new("subagent.run", false, "cancelled", "partial output")));
        var timeline = new AppConversationTimeline { Messages = messages };
        var window = new Window { Width = 740, Height = 640, Content = timeline };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Null(Assert.Single(timeline.Timeline.Turns).Response);
            Assert.DoesNotContain(timeline.GetVisualDescendants().OfType<AppToolActivityRow>(), row => row.IsEffectivelyVisible);
            Assert.DoesNotContain(timeline.GetVisualDescendants().OfType<AppResponseContent>(), view => view.IsEffectivelyVisible);
            Assert.DoesNotContain(timeline.GetVisualDescendants().OfType<AppIconButton>(), button => button.IsEffectivelyVisible);
            Assert.Equal(hasUser ? 1 : 0, timeline.GetVisualDescendants().OfType<AppBorder>()
                .Count(border => border.Classes.Contains("user-bubble") && border.IsEffectivelyVisible));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
