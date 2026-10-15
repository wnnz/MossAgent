using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolProgressCompletionTests
{
    [Fact]
    public async Task Timeout_ReportsFinalizingAndReturnsTheOriginalFailure()
    {
        var tool = new ToolProgressDelayTool();
        var phases = new List<ToolExecutionPhase>();
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly)
        {
            Timeout = TimeSpan.FromMilliseconds(100),
            Progress = report => phases.Add(report.Phase)
        };
        var executor = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var result = await executor.ExecuteAsync(new("timeout-call", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal([ToolExecutionPhase.Validating, ToolExecutionPhase.AwaitingApproval,
            ToolExecutionPhase.Executing, ToolExecutionPhase.Finalizing], phases);
    }

    [Fact]
    public async Task Finalizing_PrecedesAuditCompletionAndKeepsCancellationAndOutputBudget()
    {
        var tool = new ApprovalExecutionProbeTool();
        var audit = new ToolProgressAuditSink();
        var phases = new List<ToolExecutionPhase>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess)
        {
            MaximumOutputCharacters = 8,
            Progress = report => phases.Add(report.Phase)
        };
        var execution = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), audit)
            .ExecuteAsync(new("audit-call", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { })), context, cancellation.Token);
        try
        {
            await audit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.Equal(ToolExecutionPhase.Finalizing, phases[^1]);
            Assert.False(execution.IsCompleted);
            cancellation.Cancel();
            Assert.False(execution.IsCompleted);
            Assert.InRange(audit.Completion!.OutputLength, 0, 8);
        }
        finally
        {
            audit.Release.TrySetResult();
            await execution.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
        var result = await execution;
        Assert.True(result.IsSuccess);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(audit.Completion!.OutputLength, result.OutputCharacterCount);
        Assert.InRange(result.OutputCharacterCount, 0, 8);
    }
}
