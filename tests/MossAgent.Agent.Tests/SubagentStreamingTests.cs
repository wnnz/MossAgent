using MossAgent.Application.Agent;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentStreamingTests
{
    [Fact]
    public async Task RunAsync_PresentsChildTextBeforeTheChildModelCompletes()
    {
        var provider = new GatedSubagentModelProvider();
        var fixture = SubagentStreamingFixture.Create(provider);
        var events = new List<AgentEvent>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        await foreach (var agentEvent in fixture.Runner.RunAsync(fixture.Request, cancellation.Token))
        {
            events.Add(agentEvent);
            if (agentEvent is not AgentSubagentProgressEvent { Message.IsPartial: true } progress) continue;
            if (!provider.Continue.Task.IsCompleted)
            {
                Assert.Equal("正在", progress.Message.Content);
                Assert.False(provider.ChildCompleted);
                provider.Continue.SetResult();
            }
        }

        Assert.True(provider.ChildCompleted);
        Assert.Contains(events, agentEvent => agentEvent is AgentSubagentProgressEvent { Message.IsPartial: true });
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.True(result.IsSuccess);
        Assert.Equal("正在审查", result.Content);
        Assert.All(result.Dialogue!, message => Assert.False(message.IsPartial));
        Assert.Single(result.Dialogue!, message => message.Role == "子代理");
    }

    [Fact]
    public async Task RunAsync_CancellationPublishesFinalToolResultBeforeStopping()
    {
        var provider = new GatedSubagentModelProvider();
        var fixture = SubagentStreamingFixture.Create(provider);
        var events = new List<AgentEvent>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var agentEvent in fixture.Runner.RunAsync(fixture.Request, cancellation.Token))
            {
                events.Add(agentEvent);
                if (agentEvent is AgentSubagentProgressEvent { Message.IsPartial: true }) cancellation.Cancel();
            }
        });

        Assert.Contains(events, agentEvent => agentEvent is AgentSubagentProgressEvent { Message.IsPartial: true });
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.Contains(result.Dialogue!, message => message.Role == "子代理" && message.Content == "正在");
        Assert.All(result.Dialogue!, message => Assert.False(message.IsPartial));
        Assert.False(provider.ChildCompleted);
        Assert.Equal(1, provider.ParentTurns);
        Assert.DoesNotContain(events, agentEvent => agentEvent is AgentCompletedEvent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_AbandonedProgressCancelsAndDrainsTheChild(bool presentationFailed)
    {
        var provider = new GatedSubagentModelProvider();
        var fixture = SubagentStreamingFixture.Create(provider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var sawPartial = false;
        try
        {
            var error = await Record.ExceptionAsync(async () =>
            {
                await foreach (var agentEvent in fixture.Runner.RunAsync(fixture.Request, cancellation.Token))
                {
                    if (agentEvent is not AgentSubagentProgressEvent { Message.IsPartial: true }) continue;
                    sawPartial = true;
                    if (presentationFailed) throw new InvalidOperationException("presentation failed");
                    break;
                }
            });

            if (presentationFailed) Assert.Equal("presentation failed", Assert.IsType<InvalidOperationException>(error).Message);
            else Assert.Null(error);
            Assert.True(sawPartial);
            Assert.True(provider.ChildStopped.Task.IsCompleted);
            Assert.True(provider.ChildCancellationRequested);
            Assert.False(provider.ChildCompleted);
            Assert.False(cancellation.IsCancellationRequested);
            Assert.Equal(1, provider.ParentTurns);
        }
        finally
        {
            await cancellation.CancelAsync();
            await provider.ChildStopped.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

}
