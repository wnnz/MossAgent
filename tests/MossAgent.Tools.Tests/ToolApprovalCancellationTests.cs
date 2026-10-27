using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolApprovalCancellationTests
{
    [Fact]
    public async Task CancelWhileApprovalIsPending_IsNotAnApprovalFailure()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = new ApprovalExecutionProbeTool();
        var approvals = new PolicyApprovalService(async (_, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var running = new ToolExecutor(new ToolRegistry([tool]), approvals)
            .ExecuteAsync(Request(tool), Context(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        var result = await running;

        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task CancelAfterApproval_IsCheckedBeforeCallingANonCooperativeTool()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var tool = new ApprovalExecutionProbeTool();
        var approvals = new PolicyApprovalService(async (_, _, _) =>
        {
            await cancellation.CancelAsync();
            return true;
        });

        var result = await new ToolExecutor(new ToolRegistry([tool]), approvals)
            .ExecuteAsync(Request(tool), Context(), cancellation.Token);

        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task CancelAheadOfInFlightRevocation_RecordsAuthorizationChanged()
    {
        var lease = new RevocableWorkspaceLease();
        var context = Context() with { WorkspaceGuard = lease };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = new ApprovalExecutionProbeTool();
        var approvals = new PolicyApprovalService(async (_, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var running = new ToolExecutor(new ToolRegistry([tool]), approvals)
            .ExecuteAsync(Request(tool), context, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        lease.Revoke();

        var result = await running;

        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task CancelWithoutRevocation_KeepsToolCancelled()
    {
        var lease = new RevocableWorkspaceLease();
        var context = Context() with { WorkspaceGuard = lease };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = new ApprovalExecutionProbeTool();
        var approvals = new PolicyApprovalService(async (_, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var running = new ToolExecutor(new ToolRegistry([tool]), approvals)
            .ExecuteAsync(Request(tool), context, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        var result = await running;

        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrokenApprovalWithoutCallerCancellation_RemainsASafeFailure(bool unexpectedCancellation)
    {
        var tool = new ApprovalExecutionProbeTool();
        const string secret = "synthetic approval credential";
        var approvals = new PolicyApprovalService((_, _, _) => throw (unexpectedCancellation
            ? new OperationCanceledException(secret) : new InvalidOperationException(secret)));

        var result = await new ToolExecutor(new ToolRegistry([tool]), approvals)
            .ExecuteAsync(Request(tool), Context(), TestContext.Current.CancellationToken);

        Assert.Equal("approval_failed", result.ErrorCode);
        Assert.DoesNotContain(secret, result.Summary, StringComparison.Ordinal);
        Assert.Equal(0, tool.ExecutionCount);
    }

    private static ToolRequest Request(ApprovalExecutionProbeTool tool) =>
        new("approval-boundary", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { }));

    private static ToolExecutionContext Context() =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);
}
