using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineAllocationTests(ITestOutputHelper output)
{
    private const int Iterations = 32;

    [Theory]
    [InlineData(32)]
    [InlineData(256)]
    [InlineData(1000)]
    public void UnchangedTurns_DoNotRebuildMapsRowsOrActivitySnapshots(int turnCount)
    {
        var messages = new ObservableCollection<ChatMessageViewModel>();
        for (var index = 0; index < turnCount; index++)
        {
            messages.Add(new("你", "prompt"));
            messages.Add(ChatMessageViewModel.CreateTool(new("probe", true, "done", null)));
            messages.Add(new("MossAgent", "reply"));
        }
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, false);
        var original = timeline.Turns.ToArray();
        var notifications = 0;
        timeline.Turns.CollectionChanged += (_, _) => notifications++;
        foreach (var turn in original) turn.Activities.CollectionChanged += (_, _) => notifications++;

        var bytes = Measure(() => timeline.Bind(messages, false));

        output.WriteLine($"{turnCount} turns: {bytes / Iterations} bytes/refresh");
        Assert.Equal(original, timeline.Turns);
        Assert.Equal(0, notifications);
        Assert.InRange(bytes, 0, Iterations * 4096L);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(256, true)]
    [InlineData(32, false)]
    [InlineData(256, false)]
    public void InvisibleMessageDeltas_AddOnlyBoundedEventOverhead(int messageCount, bool move)
    {
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "prompt") };
        for (var index = 0; index < messageCount; index++) messages.Add(new("MossAgent", ""));
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(messages, true);
        var original = Assert.Single(timeline.Turns);
        var added = new ChatMessageViewModel("MossAgent", "");
        var bytes = Measure(() =>
        {
            if (move)
            {
                messages.Move(1, messageCount);
                messages.Move(messageCount, 1);
            }
            else
            {
                messages.Add(added);
                messages.RemoveAt(messages.Count - 1);
            }
        });

        output.WriteLine($"{messageCount} messages, {(move ? "move" : "add/remove")}: {bytes / Iterations} bytes/pair");
        Assert.Same(original, Assert.Single(timeline.Turns));
        Assert.Empty(original.Activities);
        Assert.Null(original.Response);
        Assert.InRange(bytes, 0, Iterations * 1024L);
    }

    private static long Measure(Action action)
    {
        for (var index = 0; index < 8; index++) action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < Iterations; index++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
