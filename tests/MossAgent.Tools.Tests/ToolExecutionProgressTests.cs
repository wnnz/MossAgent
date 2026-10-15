using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolExecutionProgressTests
{
    [Theory]
    [InlineData("approved", null, 1)]
    [InlineData("denied", "approval_denied", 0)]
    [InlineData("readonly", "capability_denied", 0)]
    [InlineData("cancel-after-approve", "tool_cancelled", 0)]
    [InlineData("cancel-on-executing", "tool_cancelled", 0)]
    [InlineData("broken-observer", null, 1)]
    [InlineData("unknown", "tool_not_found", 0)]
    public async Task Phases_PreserveApprovalCancellationAndExecution(string mode, string? errorCode, int executions)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var tool = new ApprovalExecutionProbeTool();
        var reports = new List<ToolExecutionProgress>();
        var approvals = 0;
        var approval = new PolicyApprovalService((_, _, _) =>
        {
            approvals++;
            if (mode == "cancel-after-approve") cancellation.Cancel();
            return ValueTask.FromResult(mode != "denied");
        });
        var request = new ToolRequest("progress-call", mode == "unknown" ? "unknown.tool" : tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { credential = "synthetic-secret-argument" }));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            mode == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime)
        {
            Progress = report =>
            {
                reports.Add(report);
                if (mode == "broken-observer") throw new InvalidOperationException("synthetic-observer-secret");
                if (mode == "cancel-on-executing" && report.Phase == ToolExecutionPhase.Executing) cancellation.Cancel();
            }
        };
        var result = await new ToolExecutor(new ToolRegistry([tool]), approval).ExecuteAsync(request, context, cancellation.Token);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(executions, tool.ExecutionCount);
        Assert.Equal(mode is "readonly" or "unknown" ? 0 : 1, approvals);
        var phases = reports.Select(report => report.Phase).ToArray();
        Assert.Equal(ToolExecutionPhase.Validating, phases[0]);
        Assert.Equal(ToolExecutionPhase.Finalizing, phases[^1]);
        Assert.Equal(mode is "approved" or "broken-observer" or "cancel-on-executing", phases.Contains(ToolExecutionPhase.Executing));
        Assert.Equal(mode is not ("readonly" or "unknown"), phases.Contains(ToolExecutionPhase.AwaitingApproval));
        Assert.All(reports, report =>
        {
            Assert.Equal(context.TaskId, report.TaskId);
            Assert.Equal(request.CallId, report.CallId);
            Assert.Equal(request.ToolName, report.ToolName);
            Assert.DoesNotContain("secret", report.ToString(), StringComparison.Ordinal);
        });
        Assert.DoesNotContain("observer-secret", result.Summary, StringComparison.Ordinal);
    }
}
