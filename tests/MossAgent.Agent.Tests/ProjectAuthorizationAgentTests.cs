using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ProjectAuthorizationAgentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RevocationCancelsProviderStreamWithExplicitFailureAndPreservesPartialText()
    {
        await using var scope = new ProjectAuthorizationAgentScope();
        var fixture = ToolProgressAgentFixture.Create(context: scope.Context);
        var provider = new ProjectAuthorizationStreamProvider();
        var tools = new ToolRegistry([]);
        var runner = new AgentRunner(new StubModelProviderRegistry(provider), tools,
            new ToolExecutor(tools, new FixedToolApprovalService(true)), new InMemorySubagentConfigurationRepository([]));
        var events = new List<AgentEvent>();
        async Task ReadAsync()
        {
            await foreach (var item in runner.RunAsync(fixture.Request, Token)) events.Add(item);
        }
        var running = ReadAsync();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        scope.Revoke();
        var failure = await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => running.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Equal("workspace_authorization_changed", TaskFailureClassifier.CodeFromException(failure));
        Assert.Equal("retained prefix", Assert.Single(events.OfType<AgentTextEvent>()).Delta);
        Assert.True(provider.Cancelled);
        Assert.Empty(events.OfType<AgentCompletedEvent>());
    }

    [Fact]
    public async Task RevokedToolResultIsYieldedBeforeRunFailsAndNoFurtherModelTurnIsStarted()
    {
        await using var scope = new ProjectAuthorizationAgentScope();
        var calls = 0;
        var fixture = ToolProgressAgentFixture.Create(new ToolProgressProbeExecutor((_, _, _) =>
        {
            calls++;
            scope.Revoke();
            return Task.FromResult(ToolResult.Failure("retained known effect", "workspace_authorization_changed",
                [new("tool", "retained process") ]));
        }), scope.Context);
        var events = new List<AgentEvent>();
        async Task ReadAsync()
        {
            await foreach (var item in fixture.Runner.RunAsync(fixture.Request, Token)) events.Add(item);
        }
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(ReadAsync);
        Assert.Equal(1, calls);
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.Equal("retained process", Assert.Single(result.Dialogue!).Content);
        Assert.Empty(events.OfType<AgentCompletedEvent>());
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(ReadAsync);
        Assert.Equal(1, calls);
    }
}
