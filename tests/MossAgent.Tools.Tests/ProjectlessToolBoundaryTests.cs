using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ProjectlessToolBoundaryTests
{
    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly)]
    [InlineData(ApprovalPolicy.AskEveryTime)]
    [InlineData(ApprovalPolicy.FullAccess)]
    public async Task DisabledTools_DenyEvenReadOnlyToolsBeforeApprovalOrExecution(ApprovalPolicy policy)
    {
        var tool = new ToolOutputProbeTool(ToolResult.Success("must not execute"), new InvalidOperationException());
        var prompted = false;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            prompted = true;
            return ValueTask.FromResult(true);
        }));
        var context = new ToolExecutionContext(Guid.NewGuid(), string.Empty, [], policy) { ToolsEnabled = false };
        var result = await executor.ExecuteAsync(new("call", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("capability_denied", result.ErrorCode);
        Assert.False(prompted);
    }
}
