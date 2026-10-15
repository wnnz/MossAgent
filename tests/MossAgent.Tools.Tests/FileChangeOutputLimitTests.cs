using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeOutputLimitTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(135)]
    [InlineData(136)]
    [InlineData(138)]
    [InlineData(274)]
    public async Task OutputBudget_RetainsOnlyWholeRecordsBeforeText(int budget)
    {
        var changes = new[] { Change("file.txt"), Change("next.txt") };
        var output = ToolResult.Success("ok") with { FileChanges = changes };

        var result = await ExecuteAsync(output, budget);

        Assert.InRange(result.OutputCharacterCount, 0, Math.Max(0, budget));
        var expected = Math.Min(2, Math.Max(0, budget) / 136);
        Assert.Equal(expected, result.FileChanges!.Count);
        Assert.Equal(expected < 2, result.FileChangesTruncated);
        Assert.All(result.FileChanges, change => Assert.True(change.IsValid));
        if (expected > 0) Assert.Same(changes[0], result.FileChanges[0]);
        if (expected == 2) Assert.Same(output, result);
        Assert.Equal(2, output.FileChanges!.Count);
    }

    [Theory]
    [InlineData("bad-hash")]
    [InlineData(null)]
    public async Task InvalidMetadata_IsDroppedAndMarkedIncomplete(string? after)
    {
        var output = ToolResult.Success("ok") with { FileChanges = [new("file.txt", null, after!)] };

        var result = await ExecuteAsync(output, 1000);

        Assert.Empty(result.FileChanges!);
        Assert.True(result.FileChangesTruncated);
        Assert.Equal("ok", result.Summary);
    }

    [Fact]
    public async Task InterruptedException_PreservesCommittedRecordsWithTheSameBudget()
    {
        var change = Change("file.txt");
        var exception = new ToolRunInterruptedException([new("子代理", "已写入")],
            TestContext.Current.CancellationToken, [change], true);
        var result = await ExecuteAsync(ToolResult.Success("unused"), 1000, exception);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(change, Assert.Single(result.FileChanges!));
        Assert.True(result.FileChangesTruncated);
        Assert.Single(result.Dialogue!);
    }

    private static ToolFileChange Change(string path) => new(path, new string('A', 64), new string('B', 64));

    private static Task<ToolResult> ExecuteAsync(ToolResult output, int budget, Exception? exception = null)
    {
        var tool = new ToolOutputProbeTool(output, exception);
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.FullAccess, MaximumOutputCharacters: budget);
        return executor.ExecuteAsync(new ToolRequest("change-output", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);
    }
}
