using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolExecutorOutputLimitTests
{
    [Fact]
    public async Task ExecuteAsync_LimitsMcpErrorSummaryWithAllOtherOutput()
    {
        var output = ToolResult.Failure(new string('s', 100), "mcp_tool_error") with
        {
            Content = "content",
            Dialogue = [new ToolDialogueMessage("role", "dialogue")]
        };

        var result = await ExecuteWithLimitAsync(output, 25);

        Assert.False(result.IsSuccess);
        Assert.Equal("mcp_tool_error", result.ErrorCode);
        Assert.EndsWith("[截断]", result.Summary);
        Assert.Equal(string.Empty, result.Content);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ToolDialogueMessage>>(result.Dialogue));
        Assert.True(result.OutputCharacterCount <= 25);
    }

    [Fact]
    public async Task ExecuteAsync_LimitsDialogueRolesAndMessageCount()
    {
        var output = ToolResult.Success("ok", dialogue:
        [
            new ToolDialogueMessage(new string('r', 30), new string('c', 30)),
            new ToolDialogueMessage("second", "content")
        ]);

        var result = await ExecuteWithLimitAsync(output, 15);

        var message = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ToolDialogueMessage>>(result.Dialogue));
        Assert.EndsWith("[截断]", message.Role);
        Assert.Equal(string.Empty, message.Content);
        Assert.Equal(15, result.OutputCharacterCount);
    }

    [Fact]
    public async Task ExecuteAsync_LimitsToolExceptionSummary()
    {
        const string secret = "fixture-private-token";
        var result = await ExecuteWithLimitAsync(ToolResult.Success("unused"), 25,
            new InvalidOperationException(secret + new string('e', 100)));

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_failed", result.ErrorCode);
        Assert.DoesNotContain(secret, result.Summary);
        Assert.Contains("工具执行失败", result.Summary);
        Assert.True(result.OutputCharacterCount <= 25);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesBeforeSurrogatePair()
    {
        var output = ToolResult.Success("ok", new string('a', 10) + "😀" + new string('z', 10));

        var result = await ExecuteWithLimitAsync(output, 18);

        Assert.NotNull(result.Content);
        Assert.DoesNotContain(result.Content, char.IsSurrogate);
        Assert.EndsWith("[截断]", result.Content);
        Assert.True(result.OutputCharacterCount <= 18);
    }

    [Fact]
    public async Task ExecuteAsync_InterruptedDialogueStillUsesTheSharedOutputBudget()
    {
        var interruption = new ToolRunInterruptedException(
            [new ToolDialogueMessage("子代理", new string('x', 1000))], TestContext.Current.CancellationToken);
        var result = await ExecuteWithLimitAsync(ToolResult.Success("unused"), 40, interruption);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.True(result.OutputCharacterCount <= 40);
        Assert.EndsWith("[截断]", Assert.Single(result.Dialogue!).Content);
    }

    [Fact]
    public void OutputCharacterCount_IncludesSummaryAndDialogueRoles()
    {
        var output = ToolResult.Success("sum", "body", [new ToolDialogueMessage("tool", "output")]);

        Assert.Equal(17, output.OutputCharacterCount);
    }

    [Theory]
    [InlineData(true, 17)]
    [InlineData(true, 18)]
    [InlineData(true, int.MaxValue)]
    [InlineData(false, 17)]
    [InlineData(false, 18)]
    [InlineData(false, int.MaxValue)]
    public async Task ExecuteAsync_ReusesResultAndDialogueWhenOutputFits(bool success, int maximumCharacters)
    {
        var output = new ToolResult(success, "sum", "body", ErrorCode: success ? null : "probe_failure",
            Dialogue: [new ToolDialogueMessage("tool", "output", IsPartial: true)]);

        var result = await ExecuteWithLimitAsync(output, maximumCharacters);

        Assert.Same(output, result);
        Assert.Same(output.Dialogue, result.Dialogue);
        Assert.Same(output.Dialogue![0], result.Dialogue![0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_ReusesEmptyResultAtZeroBudget(bool nullDialogue)
    {
        var output = ToolResult.Success(string.Empty, dialogue: nullDialogue ? null : []);

        var result = await ExecuteWithLimitAsync(output, 0);

        Assert.Same(output, result);
        Assert.Equal(0, result.OutputCharacterCount);
    }

    [Fact]
    public async Task ExecuteAsync_DropsEvenEmptyMessagesAfterBudgetIsExhausted()
    {
        var output = ToolResult.Success("sum", "body", [new ToolDialogueMessage(string.Empty, string.Empty)]);

        var result = await ExecuteWithLimitAsync(output, 7);

        Assert.Empty(result.Dialogue!);
        Assert.Single(output.Dialogue!);
        Assert.Equal(7, result.OutputCharacterCount);
    }

    [Fact]
    public async Task ExecuteAsync_TruncationReusesUnchangedMessagesAndRetainsMetadata()
    {
        var artifact = new ToolArtifact("snapshot", "snapshot.txt", "text/plain", 8, new string('a', 64));
        var output = ToolResult.Failure("ok", "probe_failure",
            [new("tool", "first", IsPartial: true), new("assistant", new string('x', 100)), new("tool", "last")]) with
        {
            Artifacts = [artifact]
        };

        var result = await ExecuteWithLimitAsync(output, 26);

        Assert.False(result.IsSuccess);
        Assert.Equal(output.ErrorCode, result.ErrorCode);
        Assert.Same(output.Artifacts, result.Artifacts);
        Assert.Equal(2, result.Dialogue!.Count);
        Assert.Same(output.Dialogue![0], result.Dialogue[0]);
        Assert.True(result.Dialogue[0].IsPartial);
        Assert.EndsWith("[截断]", result.Dialogue[1].Content);
        Assert.Equal(26, result.OutputCharacterCount);
        Assert.Equal(3, output.Dialogue.Count);
        Assert.Equal(100, output.Dialogue[1].Content.Length);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task ExecuteAsync_TinyAndNegativeBudgetsRemainBounded(int maximumCharacters)
    {
        var output = ToolResult.Success("prefix😀suffix", "body", [new("tool", "dialogue")]);

        var result = await ExecuteWithLimitAsync(output, maximumCharacters);

        Assert.InRange(result.OutputCharacterCount, 0, Math.Max(0, maximumCharacters));
        Assert.Empty(result.Dialogue!);
        Assert.DoesNotContain(result.Summary, char.IsSurrogate);
        Assert.Equal("prefix😀suffix", output.Summary);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOversizedAggregateWithoutCountingItAsAnInt()
    {
        var message = new ToolDialogueMessage("tool", new string('x', 1_000_000));
        var output = ToolResult.Success("ok", dialogue: Enumerable.Repeat(message, 2200).ToArray());

        var result = await ExecuteWithLimitAsync(output, 32);

        Assert.Equal(32, result.OutputCharacterCount);
        Assert.Single(result.Dialogue!);
        Assert.Equal(2200, output.Dialogue!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ReusedResultStillCompletesAuditWithArtifacts()
    {
        var audit = new ToolProgressAuditSink();
        audit.Release.TrySetResult();
        var output = ToolResult.Failure("sum", "probe_failure", [new("tool", "output")]) with
        {
            Artifacts = [new("snapshot", "snapshot.txt", "text/plain", 8, new string('a', 64))]
        };

        var result = await ExecuteWithLimitAsync(output, 32, audit: audit);

        Assert.Same(output, result);
        Assert.NotNull(audit.Completion);
        Assert.False(audit.Completion.IsSuccess);
        Assert.Equal(output.ErrorCode, audit.Completion.ErrorCode);
        Assert.Equal(output.OutputCharacterCount, audit.Completion.OutputLength);
        Assert.Same(output.Artifacts, audit.Artifacts);
    }

    private static Task<ToolResult> ExecuteWithLimitAsync(
        ToolResult output, int maximumCharacters, Exception? failure = null, IToolExecutionAuditSink? audit = null)
    {
        var tool = new ToolOutputProbeTool(output, failure);
        var executor = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), audit);
        var request = new ToolRequest("probe", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { }));
        var root = Path.GetTempPath();
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root],
            ApprovalPolicy.ReadOnly, MaximumOutputCharacters: maximumCharacters);
        return executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);
    }
}
