using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolLateCompletionTests
{
    [Fact]
    public async Task CallerCancellation_RetainsStartedWorkAndRejectsLateSuccess()
    {
        var tool = new LateCompletionProbeTool();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);
        var executor = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var execution = executor.ExecuteAsync(new("late-call", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, cancellation.Token);
        try
        {
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.True(tool.Token.IsCancellationRequested);
            Assert.False(execution.IsCompleted);
            tool.Completion.TrySetResult(ToolResult.Success("late success"));
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("tool_cancelled", result.ErrorCode);
            Assert.Equal(1, tool.ExecutionCount);
        }
        finally
        {
            tool.Completion.TrySetResult(ToolResult.Success("cleanup"));
            await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Deadline_RetainsOwnedWorkAndAuditsLateFailureWithBoundedDialogue(bool dispatchTimers, bool cancelCaller)
    {
        var tool = new LateCompletionProbeTool();
        var clock = new ManualToolTimeProvider();
        var audit = new ToolProgressAuditSink();
        audit.Release.TrySetResult();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var context = Context() with { Timeout = TimeSpan.FromMinutes(1), MaximumOutputCharacters = 32 };
        var artifact = new ToolArtifact("snapshot", "snapshot.txt", "text/plain", 8, new string('a', 64));
        var executor = Executor(tool, clock, audit);
        var execution = executor.ExecuteAsync(Request(tool), context, cancellation.Token);
        try
        {
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMinutes(2), dispatchTimers);
            if (cancelCaller) cancellation.Cancel();
            Assert.Equal(dispatchTimers || cancelCaller, tool.Token.IsCancellationRequested);
            Assert.False(execution.IsCompleted);
            var completed = ToolResult.Success("late success", dialogue: [new("assistant", new string('x', 100))]) with
            {
                Artifacts = [artifact]
            };
            tool.Completion.TrySetResult(completed);
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(cancelCaller ? "tool_cancelled" : "tool_timeout", result.ErrorCode);
            Assert.NotEmpty(result.Dialogue!);
            Assert.InRange(result.OutputCharacterCount, 0, context.MaximumOutputCharacters);
            Assert.False(audit.Completion!.IsSuccess);
            Assert.Equal(result.ErrorCode, audit.Completion.ErrorCode);
            Assert.Equal(result.OutputCharacterCount, audit.Completion.OutputLength);
            Assert.Equal(artifact, Assert.Single(result.Artifacts!));
            Assert.Equal(artifact, Assert.Single(audit.Artifacts));
            Assert.Equal(1, tool.ExecutionCount);
        }
        finally
        {
            tool.Completion.TrySetResult(ToolResult.Success("cleanup"));
            await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DeadlineElapsedDuringProgress_DoesNotStartTheToolBeforeTimerDispatch()
    {
        var tool = new LateCompletionProbeTool();
        var clock = new ManualToolTimeProvider();
        var context = Context() with
        {
            Timeout = TimeSpan.FromMinutes(1),
            Progress = progress =>
            {
                if (progress.Phase == ToolExecutionPhase.Executing) clock.Advance(TimeSpan.FromMinutes(2), false);
            }
        };

        var result = await Executor(tool, clock).ExecuteAsync(Request(tool), context, TestContext.Current.CancellationToken);

        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(0, tool.ExecutionCount);
        clock.DispatchTimers();
    }

    [Fact]
    public async Task InfiniteDeadline_DoesNotExpireOrLeaveAScheduledCallbackAfterReturn()
    {
        var tool = new LateCompletionProbeTool();
        var clock = new ManualToolTimeProvider();
        var execution = Executor(tool, clock).ExecuteAsync(Request(tool),
            Context() with { Timeout = Timeout.InfiniteTimeSpan }, TestContext.Current.CancellationToken);
        try
        {
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromDays(10));
            Assert.False(tool.Token.IsCancellationRequested);
            tool.Completion.TrySetResult(ToolResult.Success("completed"));
            Assert.True((await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).IsSuccess);
            clock.Advance(TimeSpan.FromDays(10));
            Assert.False(tool.Token.IsCancellationRequested);
        }
        finally
        {
            tool.Completion.TrySetResult(ToolResult.Success("cleanup"));
            await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ZeroDeadline_DoesNotStartTheTool()
    {
        var tool = new LateCompletionProbeTool();
        var result = await Executor(tool, new ManualToolTimeProvider()).ExecuteAsync(Request(tool),
            Context() with { Timeout = TimeSpan.Zero }, TestContext.Current.CancellationToken);

        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task ApprovalTime_DoesNotConsumeTheExecutionDeadline()
    {
        var clock = new ManualToolTimeProvider();
        var tool = new LateCompletionProbeTool();
        tool.Completion.TrySetResult(ToolResult.Success("completed"));
        var approvals = 0;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
        {
            approvals++;
            clock.Advance(TimeSpan.FromDays(1));
            return ValueTask.FromResult(true);
        }), timeProvider: clock);

        var result = await executor.ExecuteAsync(Request(tool),
            Context() with { ApprovalPolicy = ApprovalPolicy.AskEveryTime, Timeout = TimeSpan.FromMinutes(1) },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, approvals);
        Assert.Equal(1, tool.ExecutionCount);
    }

    [Fact]
    public async Task CallerCancellation_DoesNotCancelASiblingOperation()
    {
        var clock = new ManualToolTimeProvider();
        var first = new LateCompletionProbeTool();
        var second = new LateCompletionProbeTool();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var firstExecution = Executor(first, clock).ExecuteAsync(Request(first), Context(), cancelled.Token);
        var secondExecution = Executor(second, clock).ExecuteAsync(Request(second), Context(), TestContext.Current.CancellationToken);
        try
        {
            await first.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await second.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancelled.Cancel();
            Assert.True(first.Token.IsCancellationRequested);
            Assert.False(second.Token.IsCancellationRequested);
            Assert.False(firstExecution.IsCompleted);
            Assert.False(secondExecution.IsCompleted);
        }
        finally
        {
            first.Completion.TrySetResult(ToolResult.Success("first"));
            second.Completion.TrySetResult(ToolResult.Success("second"));
        }
        Assert.Equal("tool_cancelled", (await firstExecution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.True((await secondExecution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).IsSuccess);
    }

    private static ToolExecutionContext Context() => new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);
    private static ToolRequest Request(LateCompletionProbeTool tool) => new("deadline-call", tool.Descriptor.Name,
        JsonSerializer.SerializeToElement(new { }));
    private static ToolExecutor Executor(LateCompletionProbeTool tool, TimeProvider clock, IToolExecutionAuditSink? audit = null) =>
        new(new ToolRegistry([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), audit, clock);
}
