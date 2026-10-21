using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ProjectAuthorizationToolTests
{
    private static ToolRequest Request(IAgentTool tool) => new("revoked-call", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { }));

    [Fact]
    public async Task ApprovalWaitIsCancelledWithoutExecutingAndAuditCompletes()
    {
        await using var fixture = await ProjectAuthorizationToolFixture.CreateAsync();
        var tool = new ApprovalExecutionProbeTool();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approval = new PolicyApprovalService(async (_, _, token) =>
        {
            waiting.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var running = fixture.Executor(tool, approval).ExecuteAsync(Request(tool), fixture.Context, fixture.Token);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await fixture.RevokeAsync();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.Equal(0, tool.ExecutionCount);
        await fixture.AssertAuditAsync("revoked-call", null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationCancelsExecutionButRetainsKnownEffectsAndAudit(bool lateReturn)
    {
        await using var fixture = await ProjectAuthorizationToolFixture.CreateAsync();
        var tool = new LateCompletionProbeTool();
        var approval = new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true));
        var running = fixture.Executor(tool, approval).ExecuteAsync(Request(tool), fixture.Context, fixture.Token);
        await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        try
        {
            await fixture.RevokeAsync();
            try { await Task.Delay(Timeout.Infinite, tool.Token).WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
            catch (OperationCanceledException) when (tool.Token.IsCancellationRequested) { }
            Assert.True(tool.Token.IsCancellationRequested);
            if (lateReturn)
                tool.Completion.TrySetResult(ToolResult.Success("known completed effect", dialogue: [new("tool", "effect detail")]));
            else tool.Completion.TrySetException(new OperationCanceledException(tool.Token));
            var result = await running.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.Equal("workspace_authorization_changed", result.ErrorCode);
            Assert.False(result.IsSuccess);
            if (lateReturn)
            {
                Assert.Contains("known completed effect", result.Summary);
                Assert.Equal("effect detail", Assert.Single(result.Dialogue!).Content);
            }
            await fixture.AssertAuditAsync("revoked-call", true);
        }
        finally { tool.Completion.TrySetResult(ToolResult.Success("cleanup")); await running; }
    }

    [Fact]
    public async Task RevocationAfterApprovalRejectsNonCooperativeToolBeforeEntry()
    {
        await using var fixture = await ProjectAuthorizationToolFixture.CreateAsync();
        var tool = new ApprovalExecutionProbeTool();
        var approval = new PolicyApprovalService(async (_, _, _) => { await fixture.RevokeAsync(); return true; });
        var result = await fixture.Executor(tool, approval).ExecuteAsync(Request(tool), fixture.Context, fixture.Token);
        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.Equal(0, tool.ExecutionCount);
        await fixture.AssertAuditAsync("revoked-call", true);
    }
}
