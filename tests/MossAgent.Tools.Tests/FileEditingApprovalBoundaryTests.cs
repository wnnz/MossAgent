using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileEditingApprovalBoundaryTests
{
    [Theory]
    [InlineData("write", "")]
    [InlineData("replace", "")]
    [InlineData("write", "abcd")]
    [InlineData("replace", "abcd")]
    [InlineData("write", "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("replace", "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task MalformedHash_IsRejectedBeforeApprovalAndMutation(string operation, string expectedSha256)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var approvalRequests = 0;
        var tool = operation == "write" ? (IAgentTool)fixture.Write : fixture.Replace;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            approvalRequests++;
            return ValueTask.FromResult(true);
        }));
        var context = fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };

        var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool,
            new { path = fixture.Path, content = "changed", oldText = "original", newText = "changed", expectedSha256 }),
            context, fixture.Token);

        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, approvalRequests);
        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task EmptySearch_IsRejectedBeforeApproval()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var approvalRequests = 0;
        var executor = new ToolExecutor(new ToolRegistry([fixture.Replace]), new PolicyApprovalService((_, _, _) =>
        {
            approvalRequests++;
            return ValueTask.FromResult(true);
        }));
        var context = fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };

        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Replace,
            new { path = fixture.Path, oldText = "", newText = "changed" }), context, fixture.Token);

        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, approvalRequests);
        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("write", "read-only", "capability_denied")]
    [InlineData("replace", "read-only", "capability_denied")]
    [InlineData("write", "denied", "approval_denied")]
    [InlineData("replace", "denied", "approval_denied")]
    [InlineData("write", "cancelled", "tool_cancelled")]
    [InlineData("replace", "cancelled", "tool_cancelled")]
    public async Task DeniedAndCancelledRequests_DoNotChangeFiles(string operation, string policy, string errorCode)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var tool = operation == "write" ? (IAgentTool)fixture.Write : fixture.Replace;
        var approvalRequests = 0;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            approvalRequests++;
            return ValueTask.FromResult(false);
        }));
        var context = fixture.Context with
        {
            ApprovalPolicy = policy == "read-only" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (policy == "cancelled") await cancellation.CancelAsync();

        var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool,
            new { path = fixture.Path, content = "changed", oldText = "original", newText = "changed" }),
            context, cancellation.Token);

        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(policy == "denied" ? 1 : 0, approvalRequests);
        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }
}
