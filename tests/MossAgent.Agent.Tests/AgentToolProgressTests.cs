using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentToolProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingOrCancellingAfterInitialPhase_DoesNotStartExecution(bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var executions = 0;
        var executor = new ToolProgressProbeExecutor((_, _, _) =>
        {
            executions++;
            return Task.FromResult(ToolResult.Success("must not execute"));
        });
        var fixture = ToolProgressAgentFixture.Create(executor);
        await using var iterator = fixture.Runner.RunAsync(fixture.Request, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        while (await iterator.MoveNextAsync())
            if (iterator.Current is AgentToolProgressEvent { Phase: ToolExecutionPhase.Validating }) break;
        Assert.IsType<AgentToolProgressEvent>(iterator.Current);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                while (await iterator.MoveNextAsync()) { }
            });
        }
        else await iterator.DisposeAsync();
        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task PipelinePhases_PrecedeOneFinalResultWithoutArguments()
    {
        var fixture = ToolProgressAgentFixture.Create();
        var events = new List<AgentEvent>();
        await foreach (var item in fixture.Runner.RunAsync(fixture.Request, TestContext.Current.CancellationToken)) events.Add(item);
        var progress = events.OfType<AgentToolProgressEvent>().ToArray();
        Assert.Equal([ToolExecutionPhase.Validating, ToolExecutionPhase.AwaitingApproval, ToolExecutionPhase.Executing, ToolExecutionPhase.Finalizing],
            progress.Select(item => item.Phase).ToArray());
        Assert.All(progress, item => Assert.DoesNotContain("secret", item.ToString(), StringComparison.Ordinal));
        var final = Assert.Single(events.OfType<AgentToolEvent>());
        Assert.True(final.Result.IsSuccess);
        Assert.True(events.IndexOf(progress[^1]) < events.IndexOf(final));
    }

    [Fact]
    public async Task ForeignAndLatePhases_AreRejectedWithoutInvokingAnExpiredObserver()
    {
        Action<ToolExecutionProgress>? callback = null;
        ToolExecutionProgress? accepted = null;
        var observed = 0;
        var context = ToolProgressAgentFixture.Create().Request.ToolContext with { Progress = _ => observed++ };
        var executor = new ToolProgressProbeExecutor((request, scope, _) =>
        {
            callback = scope.Progress;
            accepted = new(scope.TaskId, request.CallId, request.ToolName, ToolExecutionPhase.Executing);
            callback!(accepted with { TaskId = Guid.NewGuid() });
            callback(accepted with { CallId = "foreign-call" });
            callback(accepted with { ToolName = "foreign-tool" });
            callback(accepted with { Phase = (ToolExecutionPhase)99 });
            callback(accepted);
            return Task.FromResult(ToolResult.Success("done"));
        });
        var fixture = ToolProgressAgentFixture.Create(executor, context);
        var events = new List<AgentEvent>();
        await foreach (var item in fixture.Runner.RunAsync(fixture.Request, TestContext.Current.CancellationToken)) events.Add(item);
        Assert.Equal(2, events.OfType<AgentToolProgressEvent>().Count());
        Assert.Equal(1, observed);
        callback!(accepted!);
        Assert.Equal(1, observed);
    }

    [Fact]
    public async Task ClosingTheReader_CancelsAndAwaitsNoncooperativeOwnedExecution()
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = false;
        var executor = new ToolProgressProbeExecutor(async (request, context, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            context.Progress!(new(context.TaskId, request.CallId, request.ToolName, ToolExecutionPhase.Executing));
            await released.Task;
            exited = true;
            return ToolResult.Success("owned execution returned");
        });
        var fixture = ToolProgressAgentFixture.Create(executor);
        var iterator = fixture.Runner.RunAsync(fixture.Request, TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Task? disposal = null;
        try
        {
            while (await iterator.MoveNextAsync())
                if (iterator.Current is AgentToolProgressEvent { Phase: ToolExecutionPhase.Executing }) break;
            disposal = iterator.DisposeAsync().AsTask();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.False(disposal.IsCompleted);
            Assert.False(exited);
        }
        finally
        {
            released.TrySetResult();
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            else await iterator.DisposeAsync();
        }
        Assert.True(exited);
    }
}
