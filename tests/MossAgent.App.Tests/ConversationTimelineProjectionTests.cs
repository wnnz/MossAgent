using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineProjectionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompletedTurn_CollapsesOnlyActivitiesAndRetainsOriginalMessagesAndBlocks()
    {
        var user = new ChatMessageViewModel("你", "inspect", createdAt: Start);
        var intermediate = new ChatMessageViewModel("MossAgent", "checking", createdAt: Start.AddSeconds(1));
        var tool = ChatMessageViewModel.CreateTool(new("filesystem.read_file", true, "read", "result"));
        var response = new ChatMessageViewModel("MossAgent", "# result", createdAt: Start.AddSeconds(65));
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind([user, intermediate, tool, response], false);
        var turn = Assert.Single(timeline.Turns);
        Assert.Same(user, turn.UserMessage);
        Assert.Same(response, turn.Response);
        Assert.Equal([intermediate, tool], turn.Activities);
        Assert.Equal(TimeSpan.FromSeconds(65), turn.Elapsed);
        Assert.Equal("用时 1分钟 5秒", turn.Summary);
        Assert.False(turn.IsExpanded);
        turn.ToggleCommand.Execute(null);
        Assert.True(turn.IsExpanded);
        Assert.NotEmpty(response.Blocks);
        Assert.Equal("# result", response.Content);
    }

    [Fact]
    public void LiveTurn_ReusesRowsAcrossDeltasAndCollapsesAfterTimestampedCompletion()
    {
        var user = new ChatMessageViewModel("你", "inspect", createdAt: Start);
        var tool = ChatMessageViewModel.CreateTool(new("probe", false, "running", null), isRunning: true);
        var response = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel> { user, tool, response };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var turn = Assert.Single(timeline.Turns);
        Assert.True(turn.IsExpanded);
        Assert.Null(turn.Elapsed);
        response.Append("first");
        Assert.Same(response, turn.Response);
        var collectionChanges = 0;
        timeline.Turns.CollectionChanged += (_, _) => collectionChanges++;
        response.Append(" second");
        Assert.Equal(0, collectionChanges);
        Assert.Same(turn, Assert.Single(timeline.Turns));
        response.SetCreatedAt(Start.AddSeconds(3));
        timeline.Bind(messages, false);
        Assert.Equal(TimeSpan.FromSeconds(3), turn.Elapsed);
        Assert.False(turn.IsExpanded);
        Assert.Equal("first second", turn.Response!.Content);
    }

    [Fact]
    public void MultipleTurns_KeepIdenticalPromptsSeparateAndOnlyLatestRuns()
    {
        var firstUser = new ChatMessageViewModel("你", "same", createdAt: Start);
        var firstReply = new ChatMessageViewModel("MossAgent", "one", createdAt: Start.AddSeconds(2));
        var secondUser = new ChatMessageViewModel("你", "same", createdAt: Start.AddSeconds(10));
        var secondReply = new ChatMessageViewModel("MossAgent", "two");
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind([firstUser, firstReply, secondUser, secondReply], true);
        Assert.Equal(2, timeline.Turns.Count);
        Assert.False(timeline.Turns[0].IsRunning);
        Assert.True(timeline.Turns[1].IsRunning);
        Assert.Same(firstUser, timeline.Turns[0].UserMessage);
        Assert.Same(secondUser, timeline.Turns[1].UserMessage);
        Assert.Equal(TimeSpan.FromSeconds(2), timeline.Turns[0].Elapsed);
        Assert.Null(timeline.Turns[1].Elapsed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrBackwardsTimestamps_DoNotInventDurations(bool missing)
    {
        var user = new ChatMessageViewModel("你", "inspect", createdAt: Start);
        var reply = new ChatMessageViewModel("MossAgent", "answer", createdAt: missing ? null : Start.AddSeconds(-1));
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind([user, reply], false);
        var turn = Assert.Single(timeline.Turns);
        Assert.Null(turn.Elapsed);
        Assert.False(turn.HasSummary);
        Assert.False(turn.ToggleCommand.CanExecute(null));
        Assert.Same(reply, turn.Response);
    }

    [Fact]
    public void ToolOnlyAndOrphanHistory_RemainAccessibleWithoutAFabricatedUserOrReply()
    {
        var tool = ChatMessageViewModel.CreateTool(new("probe", false, "cancelled", "details"));
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind([tool], false);
        var turn = Assert.Single(timeline.Turns);
        Assert.Null(turn.UserMessage);
        Assert.Null(turn.Response);
        Assert.False(turn.HasTimestamp);
        Assert.Null(turn.Elapsed);
        Assert.Same(tool, Assert.Single(turn.Activities));
        turn.ToggleCommand.Execute(null);
        Assert.True(turn.IsExpanded);
    }

    [Fact]
    public void InsertMoveAndReplace_ReconcileTurnRangesWithoutDuplicatingMessages()
    {
        var orphan = ChatMessageViewModel.CreateTool(new("orphan", true, "done", null));
        var firstUser = new ChatMessageViewModel("你", "first");
        var firstReply = new ChatMessageViewModel("MossAgent", "one");
        var secondUser = new ChatMessageViewModel("你", "second");
        var secondReply = new ChatMessageViewModel("MossAgent", "two");
        var messages = new ObservableCollection<ChatMessageViewModel>
            { orphan, firstUser, firstReply, secondUser, secondReply };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var originalTurns = timeline.Turns.ToArray();
        var intermediate = new ChatMessageViewModel("MossAgent", "checking");
        messages.Insert(2, intermediate);
        Assert.Same(intermediate, Assert.Single(timeline.Turns[1].Activities));
        messages.Move(2, 4);
        Assert.Empty(timeline.Turns[1].Activities);
        Assert.Same(intermediate, Assert.Single(timeline.Turns[2].Activities));
        var cancelled = ChatMessageViewModel.CreateTool(new("probe", false, "cancelled", null));
        messages[^1] = cancelled;
        Assert.Equal(originalTurns, timeline.Turns);
        Assert.Same(firstReply, timeline.Turns[1].Response);
        Assert.Null(timeline.Turns[2].Response);
        Assert.Equal([intermediate, cancelled], timeline.Turns[2].Activities);
        secondReply.Content = "late";
        Assert.Null(timeline.Turns[2].Response);
    }

    [Fact]
    public void RemovingAndReinsertingATurn_DoesNotResurrectItsOldDisclosureState()
    {
        var firstUser = new ChatMessageViewModel("你", "first");
        var firstReply = new ChatMessageViewModel("MossAgent", "first reply");
        var secondUser = new ChatMessageViewModel("你", "second");
        var tool = ChatMessageViewModel.CreateTool(new("probe", true, "done", null));
        var secondReply = new ChatMessageViewModel("MossAgent", "second reply");
        var messages = new ObservableCollection<ChatMessageViewModel> { firstUser, firstReply, secondUser, tool, secondReply };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var firstTurn = timeline.Turns[0];
        var removedTurn = timeline.Turns[1];
        removedTurn.ToggleCommand.Execute(null);
        Assert.True(removedTurn.IsExpanded);

        messages.RemoveAt(4);
        messages.RemoveAt(3);
        messages.RemoveAt(2);
        Assert.Same(firstTurn, Assert.Single(timeline.Turns));
        messages.Add(secondUser);
        messages.Add(tool);
        messages.Add(secondReply);

        Assert.Same(firstTurn, timeline.Turns[0]);
        Assert.NotSame(removedTurn, timeline.Turns[1]);
        Assert.False(timeline.Turns[1].IsExpanded);
        Assert.Same(secondReply, timeline.Turns[1].Response);
    }

    [Fact]
    public void TrailingResponseTransitions_ReconcileActivitiesWithoutChangingMessageIdentity()
    {
        var intermediate = new ChatMessageViewModel("MossAgent", "checking");
        var response = new ChatMessageViewModel("MossAgent", "reply");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), intermediate, response };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var turn = Assert.Single(timeline.Turns);
        var trailing = new ChatMessageViewModel("MossAgent", "");

        messages.Add(trailing);
        Assert.Null(turn.Response);
        Assert.Equal([intermediate, response], turn.Activities);
        trailing.Content = "final";
        Assert.Same(trailing, turn.Response);
        intermediate.Content = "";
        Assert.Same(response, Assert.Single(turn.Activities));
        response.Content = "";
        Assert.Empty(turn.Activities);
        messages.RemoveAt(3);
        response.Content = "restored";
        Assert.Same(turn, Assert.Single(timeline.Turns));
        Assert.Same(response, turn.Response);
        Assert.Empty(turn.Activities);
    }
}
