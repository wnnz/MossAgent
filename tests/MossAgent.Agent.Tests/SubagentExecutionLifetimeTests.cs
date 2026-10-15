using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentExecutionLifetimeTests
{
    [Fact]
    public async Task DisposeAsync_WaitsForCancelledToolAuditWithoutCancellingTheParent()
    {
        var provider = new GatedSubagentModelProvider();
        var audit = new GatedSubagentAuditSink();
        var fixture = SubagentStreamingFixture.Create(provider, audit);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var enumerator = fixture.Runner.RunAsync(fixture.Request, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Task? disposal = null;
        try
        {
            await MoveToPartialAsync(enumerator);
            disposal = enumerator.DisposeAsync().AsTask();
            var completion = await audit.CompletionStarted.Task.WaitAsync(cancellation.Token);

            Assert.False(disposal.IsCompleted);
            Assert.True(provider.ChildStopped.Task.IsCompleted);
            Assert.Equal(0, provider.ActiveChildren);
            Assert.False(cancellation.IsCancellationRequested);
            Assert.Equal("tool_cancelled", completion.ErrorCode);
            Assert.False(completion.IsSuccess);
            Assert.True(completion.ApprovalGranted);
            Assert.True(completion.OutputLength > 0);
            Assert.Equal(fixture.Request.ToolContext.TaskId, Assert.Single(audit.Starts).TaskId);
            Assert.False(audit.CompletionToken.CanBeCanceled);

            audit.ReleaseCompletion.SetResult();
            await disposal.WaitAsync(cancellation.Token);
            Assert.True(audit.IsCompleted);
        }
        finally
        {
            audit.ReleaseCompletion.TrySetResult();
            await cancellation.CancelAsync();
            if (disposal is not null) await disposal;
            else await enumerator.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_StopsOnlyItsOwnChildWhenAnotherTaskIsRunning()
    {
        var provider = new GatedSubagentModelProvider();
        var fixture = SubagentStreamingFixture.Create(provider);
        var context = fixture.Request.ToolContext;
        var otherRequest = fixture.Request with
        {
            Messages = [new ModelMessage(ModelRole.User, "另一个任务")],
            ToolContext = new ToolExecutionContext(Guid.NewGuid(), context.WorkingDirectory,
                context.AuthorizedRoots, context.ApprovalPolicy)
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var first = fixture.Runner.RunAsync(fixture.Request, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var second = fixture.Runner.RunAsync(otherRequest, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        try
        {
            await MoveToPartialAsync(first);
            await MoveToPartialAsync(second);
            Assert.Equal(2, provider.ActiveChildren);

            await first.DisposeAsync();
            Assert.Equal(1, provider.ActiveChildren);
            Assert.False(cancellation.IsCancellationRequested);
            Assert.False(provider.ChildCompleted);

            provider.Continue.SetResult();
            var events = new List<AgentEvent>();
            while (await second.MoveNextAsync()) events.Add(second.Current);
            Assert.True(Assert.Single(events.OfType<AgentToolEvent>()).Result.IsSuccess);
            Assert.Contains(events, agentEvent => agentEvent is AgentCompletedEvent);
            Assert.True(provider.ChildCompleted);
            Assert.Equal(0, provider.ActiveChildren);
            Assert.Equal(3, provider.ParentTurns);
        }
        finally
        {
            await cancellation.CancelAsync();
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    private static async Task MoveToPartialAsync(IAsyncEnumerator<AgentEvent> enumerator)
    {
        while (await enumerator.MoveNextAsync())
            if (enumerator.Current is AgentSubagentProgressEvent { Message.IsPartial: true }) return;
        Assert.Fail("Expected a live child preview before the model completed.");
    }
}
