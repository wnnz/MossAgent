using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolWorkspaceGuardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardDenialBeforeOrAfterApprovalNeverExecutesAndCompletesSafeAudit(bool afterApproval)
    {
        var tool = new WorkspaceGuardTool();
        var guard = new WorkspaceGuardProbe { Handler = (count, _) => count >= (afterApproval ? 2 : 1)
            ? Task.FromException(new UnauthorizedAccessException("synthetic-secret-token")) : Task.CompletedTask };
        var approvals = 0;
        var audit = new ToolProgressAuditSink();
        audit.Release.SetResult();
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
        { approvals++; return ValueTask.FromResult(true); }), audit);
        var result = await executor.ExecuteAsync(Request(), Context(guard), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("workspace_denied", result.ErrorCode);
        Assert.DoesNotContain("synthetic-secret-token", result.Summary);
        Assert.Equal(afterApproval ? 1 : 0, approvals);
        Assert.Equal(0, tool.Executions);
        Assert.Equal(afterApproval ? 2 : 1, guard.Checks);
        Assert.Equal("workspace_denied", audit.Completion?.ErrorCode);
        Assert.False(audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task AcceptedGuardIsCheckedAgainAfterApprovalAndPreservedByContextCopies()
    {
        var tool = new WorkspaceGuardTool();
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => Task.CompletedTask };
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var context = Context(guard) with { MaximumOutputCharacters = 1024 };
        var result = await executor.ExecuteAsync(Request(), context, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, guard.Checks);
        Assert.Equal(1, tool.Executions);
        Assert.Same(guard, context.WorkspaceGuard);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrDeadlineWaitsForGuardCleanupAndNeverRequestsApproval(bool timeout)
    {
        var tool = new WorkspaceGuardTool();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var guard = new WorkspaceGuardProbe { Handler = async (_, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { cleaning.SetResult(); await release.Task; }
        } };
        var clock = new MossAgent.Testing.ManualToolTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
            throw new InvalidOperationException("Approval must not run.")), timeProvider: clock);
        var execution = executor.ExecuteAsync(Request(), Context(guard) with { Timeout = TimeSpan.FromSeconds(5) }, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (timeout) clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
        }
        finally { release.TrySetResult(); }
        var result = await execution;
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Equal(0, tool.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchemaAndCapabilityRejectionDoNotInvokeTheWorkspaceGuard(bool invalidSchema)
    {
        var tool = new WorkspaceGuardTool();
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => throw new InvalidOperationException("Must not run.") };
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var request = invalidSchema ? Request() with { Arguments = JsonSerializer.SerializeToElement(new { unexpected = true }) } : Request();
        var result = await executor.ExecuteAsync(request, Context(guard) with { ToolsEnabled = invalidSchema }, TestContext.Current.CancellationToken);
        Assert.Equal(invalidSchema ? "invalid_arguments" : "capability_denied", result.ErrorCode);
        Assert.Equal(0, guard.Checks);
        Assert.Equal(0, tool.Executions);
    }

    private static ToolRequest Request() => new("workspace-guard", "fixture.workspace", JsonSerializer.SerializeToElement(new { }));
    private static ToolExecutionContext Context(ITaskWorkspaceGuard guard) =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime) { WorkspaceGuard = guard };
}
