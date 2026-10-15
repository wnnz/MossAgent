using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineLifetimeTests
{
    [Fact]
    public void SourceReplacementDetachAndDisposal_RejectLateUpdatesWithoutTouchingMessages()
    {
        var oldUser = new ChatMessageViewModel("你", "old");
        var oldReply = new ChatMessageViewModel("MossAgent", "old reply");
        var oldMessages = new ObservableCollection<ChatMessageViewModel> { oldUser, oldReply };
        var newUser = new ChatMessageViewModel("你", "new");
        var newMessages = new ObservableCollection<ChatMessageViewModel> { newUser };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(oldMessages, true);
        timeline.Bind(newMessages, false);
        oldReply.Content = "";
        oldMessages.Add(new("你", "late"));
        Assert.Same(newUser, Assert.Single(timeline.Turns).UserMessage);
        timeline.Detach();
        newMessages.Add(new("MossAgent", "detached"));
        Assert.Empty(timeline.Turns);
        timeline.Bind(newMessages, false);
        Assert.Equal("detached", Assert.Single(timeline.Turns).Response!.Content);
        timeline.Dispose();
        newMessages.Clear();
        timeline.Bind(oldMessages, false);
        Assert.Empty(timeline.Turns);
        Assert.Equal(3, oldMessages.Count);
    }

    [Fact]
    public void AnExplicitActivityChoice_SurvivesCompletionAndCollectionChanges()
    {
        var messages = new ObservableCollection<ChatMessageViewModel>
        {
            new("你", "prompt"),
            ChatMessageViewModel.CreateTool(new("probe", true, "done", "details"))
        };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var turn = Assert.Single(timeline.Turns);
        turn.ToggleCommand.Execute(null);
        turn.ToggleCommand.Execute(null);
        messages.Add(new("MossAgent", "reply"));
        timeline.Bind(messages, false);
        Assert.True(turn.IsExpanded);
        Assert.Same(turn, Assert.Single(timeline.Turns));
        messages.RemoveAt(1);
        Assert.Empty(turn.Activities);
        Assert.False(turn.ToggleCommand.CanExecute(null));
        messages.Clear();
        Assert.Empty(timeline.Turns);
    }

    [Fact]
    public void UnchangedHistory_DoesNotRepublishVisibilityOrCommandState()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new ObservableCollection<ChatMessageViewModel>
        {
            new("你", "prompt", createdAt: now),
            ChatMessageViewModel.CreateTool(new("probe", true, "done", "details")),
            new("MossAgent", "reply", createdAt: now.AddSeconds(3))
        };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var turn = Assert.Single(timeline.Turns);
        var notifications = new List<string?>();
        var commandChanges = 0;
        turn.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        turn.ToggleCommand.CanExecuteChanged += (_, _) => commandChanges++;
        timeline.Bind(messages, false);
        messages.Add(new("你", "next"));
        messages.Add(new("MossAgent", "next reply"));
        Assert.Empty(notifications);
        Assert.Equal(0, commandChanges);
        Assert.Same(turn, timeline.Turns[0]);
    }

    [Fact]
    public void ActivityAvailability_RepublishesOnlyWhenItChanges()
    {
        var user = new ChatMessageViewModel("你", "prompt");
        var messages = new ObservableCollection<ChatMessageViewModel> { user };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var turn = Assert.Single(timeline.Turns);
        var notifications = new List<string?>();
        var commandChanges = 0;
        turn.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        turn.ToggleCommand.CanExecuteChanged += (_, _) => commandChanges++;
        messages.Add(ChatMessageViewModel.CreateTool(new("first", true, "done", null)));
        messages.Add(ChatMessageViewModel.CreateTool(new("second", true, "done", null)));
        messages.RemoveAt(1);
        messages.RemoveAt(1);
        Assert.Equal(2, notifications.Count(name => name == nameof(ConversationTurnViewModel.HasActivities)));
        Assert.Equal(2, notifications.Count(name => name == nameof(ConversationTurnViewModel.HasSummary)));
        Assert.Equal(2, commandChanges);
        Assert.False(turn.ToggleCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0, "用时 0秒")]
    [InlineData(59, "用时 59秒")]
    [InlineData(60, "用时 1分钟 0秒")]
    [InlineData(3661, "用时 1小时 1分钟 1秒")]
    [InlineData(172800, "用时 48小时 0分钟 0秒")]
    public void Durations_AreLocalizedAndDoNotWrapHours(int seconds, string expected) =>
        Assert.Equal(expected, ConversationTimelineText.Elapsed(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void CalendarLabels_UseTheSpecifiedTimeZoneAndRecordedTime()
    {
        var now = new DateTimeOffset(2026, 10, 12, 16, 30, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("fixture", TimeSpan.FromHours(8), "fixture", "fixture");
        Assert.Equal("今天 00:05", ConversationTimelineText.Timestamp(now.AddMinutes(-25), now, zone));
        Assert.Equal("昨天 23:30", ConversationTimelineText.Timestamp(now.AddHours(-1), now, zone));
        Assert.Equal("2026/10/11 00:30", ConversationTimelineText.Timestamp(now.AddDays(-2), now, zone));
        Assert.Equal("", ConversationTimelineText.Timestamp(null, now, zone));
    }
}
