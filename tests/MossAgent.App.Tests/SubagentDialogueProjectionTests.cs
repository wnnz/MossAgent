using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentDialogueProjectionTests(ITestOutputHelper output)
{
    [Fact]
    public void RepeatedReads_RetainRowsAndParsedBlocks()
    {
        var message = CreateMessage([new("子代理", "# Review\n\n```csharp\nint value = 42;\n```")]);
        var entries = message.DialogueEntries;
        var blocks = entries[0].Blocks;

        Assert.Same(entries, message.DialogueEntries);
        Assert.Same(blocks, message.DialogueEntries[0].Blocks);
    }

    [Fact]
    public void Progress_ReusesUnchangedRowsWithoutMutatingPreviousSnapshots()
    {
        var message = CreateMessage([new("子代理", "# Earlier"), new("子代理", "Partial", true)]);
        var previous = message.DialogueEntries;
        var blocks = previous[0].Blocks;

        message.UpdateTool(message.Tool! with
        {
            Dialogue = [new("子代理", "# Earlier"), new("子代理", "Complete")]
        }, false);
        var current = message.DialogueEntries;

        Assert.Same(previous[0], current[0]);
        Assert.Same(blocks, current[0].Blocks);
        Assert.NotSame(previous[1], current[1]);
        Assert.Equal("Partial", previous[1].Content);
        Assert.Equal("Complete", current[1].Content);
    }

    [Fact]
    public void MetadataOnlyUpdate_RetainsTheProjection()
    {
        var message = CreateMessage([new("子代理", "Answer")]);
        var entries = message.DialogueEntries;

        message.UpdateTool(message.Tool! with { IsSuccess = true, Summary = "Completed" }, false);

        Assert.Same(entries, message.DialogueEntries);
    }

    [Fact]
    public void WarmReads_DoNotAllocate()
    {
        var message = CreateMessage([new("子代理", "# Review\n\n```csharp\nint value = 42;\n```")]);
        for (var iteration = 0; iteration < 100; iteration++) _ = message.DialogueEntries;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        for (var iteration = 0; iteration < 1000; iteration++) count += message.DialogueEntries.Count;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"Warm dialogue reads: {allocated} bytes / 1000 reads.");
        Assert.Equal(1000, count);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearedDialogue_DoesNotRetainOldAnswers(bool useNull)
    {
        var message = CreateMessage([new("子代理", "Previous answer")]);
        var previous = message.DialogueEntries;

        message.UpdateTool(message.Tool! with { Dialogue = useNull ? null : [] }, false);

        Assert.Empty(message.DialogueEntries);
        Assert.False(message.Tool!.HasDialogue);
        Assert.False(message.IsToolExpanded);
        Assert.Equal("Previous answer", Assert.Single(previous).Content);
    }

    [Fact]
    public void EqualSnapshotsAndPartialFlags_RetainOnlyPresentationState()
    {
        var message = CreateMessage([new("子代理", "Answer", true)]);
        var entries = message.DialogueEntries;
        var previousTool = message.Tool!;
        message.UpdateTool(previousTool with { Dialogue = [new("子代理", "Answer")] }, false);

        Assert.Same(entries, message.DialogueEntries);
        Assert.True(Assert.Single(previousTool.Dialogue!).IsPartial);
        Assert.False(Assert.Single(message.Tool!.Dialogue!).IsPartial);
    }

    [Fact]
    public void ChangedRole_DoesNotReuseItsAnswerRenderer()
    {
        var message = CreateMessage([new("子代理", "# Answer")]);
        var previous = Assert.Single(message.DialogueEntries);
        Assert.NotEmpty(previous.Blocks);

        message.UpdateTool(message.Tool! with { Dialogue = [new("工具 · filesystem.read_file", "# Answer")] }, false);
        var current = Assert.Single(message.DialogueEntries);

        Assert.NotSame(previous, current);
        Assert.True(current.IsTool);
        Assert.Empty(current.Blocks);
        Assert.True(previous.IsAnswer);
    }

    [Fact]
    public void IndependentMessages_DoNotShareMutableProjectionState()
    {
        var first = CreateMessage([new("子代理", "First answer")]);
        var second = ChatMessageViewModel.CreateTool(first.Tool!, "other-task", true);
        var firstEntries = first.DialogueEntries;
        var secondEntries = second.DialogueEntries;

        Assert.NotSame(firstEntries, secondEntries);
        Assert.NotSame(firstEntries[0], secondEntries[0]);
        first.UpdateTool(first.Tool! with { Dialogue = [new("子代理", "Changed answer")] }, false);

        Assert.Equal("Changed answer", first.DialogueEntries[0].Content);
        Assert.Same(secondEntries, second.DialogueEntries);
        Assert.Equal("First answer", second.DialogueEntries[0].Content);
    }

    [Fact]
    public void Notifications_ReadTheCurrentSnapshotAndSkipMetadataOnlyChanges()
    {
        var message = CreateMessage([new("子代理", "First answer")]);
        var observed = new List<IReadOnlyList<SubagentDialogueEntryViewModel>>();
        message.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(message.DialogueEntries)) observed.Add(message.DialogueEntries);
        };
        message.UpdateTool(message.Tool! with { Summary = "New status" }, true);
        message.UpdateTool(message.Tool! with { Dialogue = [new("子代理", "Completed answer")] }, false);

        Assert.Equal("Completed answer", Assert.Single(Assert.Single(observed)).Content);
    }

    [Fact]
    public void ShortTransitions_PreserveOrderingDuplicatesAndPreviousSnapshots()
    {
        string[] sequences = ["", "a", "b", "aa", "ab", "ba", "aaa", "aba", "bab"];
        foreach (var original in sequences)
            foreach (var replacement in sequences)
            {
                var message = CreateMessage(original.Select(value => new ToolDialogueMessage("状态", value.ToString())).ToArray());
                var previous = message.DialogueEntries;
                message.UpdateTool(message.Tool! with
                {
                    Dialogue = replacement.Select(value => new ToolDialogueMessage("状态", value.ToString())).ToArray()
                }, true);
                var current = message.DialogueEntries;
                Assert.Equal(original.Select(value => value.ToString()), previous.Select(entry => entry.Content));
                Assert.Equal(replacement.Select(value => value.ToString()), current.Select(entry => entry.Content));
                for (var index = 0; index < Math.Min(previous.Count, current.Count); index++)
                {
                    if (original[index] == replacement[index]) Assert.Same(previous[index], current[index]);
                    else Assert.NotSame(previous[index], current[index]);
                }
            }
    }

    [Fact]
    public void RowCreation_DoesNotParseAnUnobservedAnswer()
    {
        var source = new ToolDialogueMessage("子代理", string.Concat(Enumerable.Repeat("# Heading\n\n", 1000)));
        for (var iteration = 0; iteration < 100; iteration++) _ = new SubagentDialogueEntryViewModel(source);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var entry = new SubagentDialogueEntryViewModel(source);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"Unobserved answer row: {allocated} bytes.");
        Assert.InRange(allocated, 1, 128);
        Assert.Equal(1000, entry.Blocks.Count);
        Assert.Same(entry.Blocks, entry.Blocks);
    }

    private static ChatMessageViewModel CreateMessage(IReadOnlyList<ToolDialogueMessage> dialogue) =>
        ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", false, "Running", null, dialogue),
            "projection-call", true);
}
