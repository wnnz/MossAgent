using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineSubscriptionTests
{
    [Fact]
    public void DuplicateMessages_RemainObservedUntilTheirLastRemoval()
    {
        var duplicate = new ChatMessageViewModel("MossAgent", "checking");
        var reply = new ChatMessageViewModel("MossAgent", "reply");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), duplicate, duplicate, reply };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var turn = Assert.Single(timeline.Turns);
        Assert.Equal([duplicate, duplicate], turn.Activities);

        messages.RemoveAt(1);
        duplicate.Content = "";
        Assert.Empty(turn.Activities);
        duplicate.Content = "still observed";
        Assert.Same(duplicate, Assert.Single(turn.Activities));
        messages.RemoveAt(1);
        duplicate.Content = "late";
        Assert.Empty(turn.Activities);
        Assert.Same(reply, turn.Response);
    }

    [Fact]
    public void ReplacementWithAnExistingMessage_PreservesItsSubscription()
    {
        var outgoing = new ChatMessageViewModel("MossAgent", "outgoing");
        var retained = new ChatMessageViewModel("MossAgent", "retained");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), outgoing, retained };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var turn = Assert.Single(timeline.Turns);

        messages[1] = retained;
        messages[1] = retained;
        messages.RemoveAt(2);
        retained.Content = "";
        Assert.Null(turn.Response);
        retained.Content = "new reply";
        Assert.Same(retained, turn.Response);
        outgoing.Content = "late";
        Assert.Same(retained, turn.Response);
        Assert.Empty(turn.Activities);
    }

    [Fact]
    public void Move_KeepsSubscriptionsAndUpdatesActivityOrder()
    {
        var first = new ChatMessageViewModel("MossAgent", "first");
        var second = new ChatMessageViewModel("MossAgent", "second");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), first, second, new("MossAgent", "reply") };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var turn = Assert.Single(timeline.Turns);

        messages.Move(1, 2);
        Assert.Equal([second, first], turn.Activities);
        first.Content = "";
        Assert.Same(second, Assert.Single(turn.Activities));
        second.Content = "";
        Assert.Empty(turn.Activities);
        first.Content = "restored";
        Assert.Same(first, Assert.Single(turn.Activities));
    }

    [Fact]
    public void Reset_DetachesDuplicatesAndObservesLaterInsertions()
    {
        var message = new ChatMessageViewModel("MossAgent", "reply");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), message, message };
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);

        messages.Clear();
        message.Content = "late";
        Assert.Empty(timeline.Turns);
        messages.Add(new("你", "new prompt"));
        messages.Add(message);
        message.Content = "";
        Assert.Null(Assert.Single(timeline.Turns).Response);
        message.Content = "new reply";
        Assert.Same(message, Assert.Single(timeline.Turns).Response);
    }

    [Fact]
    public void DisposingOneTimeline_DoesNotDetachAnotherOwner()
    {
        var reply = new ChatMessageViewModel("MossAgent", "reply");
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt"), reply };
        using var first = new ConversationTimelineViewModel();
        using var second = new ConversationTimelineViewModel();
        first.Bind(messages, true);
        second.Bind(messages, true);

        first.Dispose();
        reply.Content = "";
        Assert.Empty(first.Turns);
        Assert.Null(Assert.Single(second.Turns).Response);
        reply.Content = "continued";
        Assert.Same(reply, Assert.Single(second.Turns).Response);
    }
}
