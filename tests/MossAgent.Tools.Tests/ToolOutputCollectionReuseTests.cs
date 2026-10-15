using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolOutputCollectionReuseTests
{
    [Fact]
    public async Task TruncatedText_RetainsCompleteFileCollectionAndOnlyReadsEachRecordOnce()
    {
        var changes = new CountingToolOutputList<ToolFileChange>([Change("first.txt"), Change("second.txt")]);
        var output = ToolResult.Success(new string('x', 100)) with { FileChanges = changes };
        var budget = changes.Items.Sum(change => change.OutputCharacterCount) + 8;

        var result = await ExecuteAsync(output, budget);

        Assert.Same(changes, result.FileChanges);
        Assert.Equal(changes.Count, changes.Reads);
        Assert.False(result.FileChangesTruncated);
        Assert.EndsWith("[截断]", result.Summary);
        Assert.Equal(100, output.Summary.Length);
        Assert.InRange(result.OutputCharacterCount, 0, budget);
    }

    [Fact]
    public async Task TruncatedFileCollection_RetainsCompleteDialogueWithoutCopying()
    {
        var changes = new[] { Change("first.txt"), new ToolFileChange("invalid.txt", null, "invalid"), Change("last.txt") };
        var dialogue = new CountingToolOutputList<ToolDialogueMessage>([new("tool", "first"), new("assistant", "second")]);
        var output = ToolResult.Success("ok", "content", dialogue) with { FileChanges = changes };

        var result = await ExecuteAsync(output, 1000);

        Assert.Same(changes[0], Assert.Single(result.FileChanges!));
        Assert.True(result.FileChangesTruncated);
        Assert.Same(dialogue, result.Dialogue);
        Assert.Equal(dialogue.Count, dialogue.Reads);
        Assert.Equal("ok", result.Summary);
        Assert.Equal("content", result.Content);
        Assert.Equal(3, changes.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialDialogue_PreservesWholePrefixAndUntouchedFileCollection(bool truncateRole)
    {
        var changes = new[] { Change("first.txt") };
        var prefix = new ToolDialogueMessage("tool", "first", IsPartial: true);
        var partial = truncateRole ? new ToolDialogueMessage(new string('r', 30), "body")
            : new ToolDialogueMessage("assistant", new string('c', 30), IsPartial: true);
        var dialogue = new[] { prefix, partial, new ToolDialogueMessage("tool", "omitted") };
        var artifact = new ToolArtifact("snapshot", "snapshot.txt", "text/plain", 8, new string('a', 64));
        var output = ToolResult.Failure("ok", "fixture_failure", dialogue) with { FileChanges = changes, Artifacts = [artifact] };
        var budget = changes[0].OutputCharacterCount + 2 + prefix.Role.Length + prefix.Content.Length + 16;

        var result = await ExecuteAsync(output, budget);

        Assert.Same(changes, result.FileChanges);
        Assert.Equal(2, result.Dialogue!.Count);
        Assert.Same(prefix, result.Dialogue[0]);
        Assert.NotSame(partial, result.Dialogue[1]);
        Assert.Equal(partial.IsPartial, result.Dialogue[1].IsPartial);
        Assert.EndsWith("[截断]", truncateRole ? result.Dialogue[1].Role : result.Dialogue[1].Content);
        Assert.Same(output.Artifacts, result.Artifacts);
        Assert.Equal(output.ErrorCode, result.ErrorCode);
        Assert.False(result.IsSuccess);
        Assert.Equal(3, dialogue.Length);
        Assert.InRange(result.OutputCharacterCount, 0, budget);
    }

    [Fact]
    public async Task ExactPrefixBudget_DropsFollowingEmptyRowsWithoutChangingSource()
    {
        var prefix = new ToolDialogueMessage("tool", "first");
        var dialogue = new[] { prefix, new ToolDialogueMessage(string.Empty, string.Empty) };
        var output = ToolResult.Success("ok", dialogue: dialogue);

        var result = await ExecuteAsync(output, 11);

        Assert.Same(prefix, Assert.Single(result.Dialogue!));
        Assert.Equal(2, dialogue.Length);
        Assert.Equal(11, result.OutputCharacterCount);
    }

    [Fact]
    public async Task PreviouslyTruncatedMetadata_StillMarksReusedCollectionsIncomplete()
    {
        var changes = new[] { Change("first.txt") };
        var output = ToolResult.Success(new string('x', 100)) with { FileChanges = changes, FileChangesTruncated = true };

        var result = await ExecuteAsync(output, changes[0].OutputCharacterCount + 8);

        Assert.Same(changes, result.FileChanges);
        Assert.True(result.FileChangesTruncated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullOrInvalidFirstRecord_DropsEntireUntrustedSuffix(bool nullRecord)
    {
        var changes = new[] { nullRecord ? null! : new ToolFileChange("invalid.txt", null, null), Change("later.txt") };
        var dialogue = new[] { new ToolDialogueMessage("tool", "retained") };
        var output = ToolResult.Success("ok", dialogue: dialogue) with { FileChanges = changes };

        var result = await ExecuteAsync(output, 1000);

        Assert.Empty(result.FileChanges!);
        Assert.True(result.FileChangesTruncated);
        Assert.Same(dialogue, result.Dialogue);
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public async Task IntactOutput_UsesOnePassAndReturnsOriginalResult()
    {
        var changes = new CountingToolOutputList<ToolFileChange>([Change("first.txt"), Change("last.txt")]);
        var dialogue = new CountingToolOutputList<ToolDialogueMessage>([new("tool", "first"), new("assistant", "second")]);
        var output = ToolResult.Success("ok", "content", dialogue) with { FileChanges = changes };

        var result = await ExecuteAsync(output, 1000);

        Assert.Same(output, result);
        Assert.Equal(changes.Count, changes.Reads);
        Assert.Equal(dialogue.Count, dialogue.Reads);
    }

    private static ToolFileChange Change(string path) => new(path, new string('A', 64), new string('B', 64));

    private static Task<ToolResult> ExecuteAsync(ToolResult output, int budget)
    {
        var tool = new ToolOutputProbeTool(output);
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var root = Path.GetTempPath();
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.ReadOnly, MaximumOutputCharacters: budget);
        return executor.ExecuteAsync(new ToolRequest("collection-reuse", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);
    }
}
