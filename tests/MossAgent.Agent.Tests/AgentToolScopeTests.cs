using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentToolScopeTests
{
    [Fact]
    public async Task UnsolicitedModelCallsStillReachTheRealPipelineAndCannotEscapeTheAdvertisedScope()
    {
        var tool = new CountingAgentTool();
        var registry = new ToolRegistry([tool]);
        var requests = new List<ModelRequest>();
        var provider = new StubModelProvider(ProviderProtocol.OpenAiResponses, [], responseFactory: request =>
        {
            requests.Add(request);
            return requests.Count == 1
                ? [new ToolCallCompleted("unadvertised", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { }))]
                : [new TextDelta("denial observed")];
        });
        var runner = new AgentRunner(new StubModelProviderRegistry(provider), registry,
            new ToolExecutor(registry, new PolicyApprovalService((_, _, _) =>
                throw new InvalidOperationException("Denied tools cannot ask approval."))), new InMemorySubagentConfigurationRepository([]));
        var configuration = new AiProvider(Guid.NewGuid(), "fixture", provider.Protocol,
            new Uri("https://example.invalid/v1"), "synthetic-only", null, false, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), configuration.Id, "fixture", "fixture", 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
        var request = new AgentRunRequest(configuration, model, [new(ModelRole.User, "fixture")],
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [], ApprovalPolicy.FullAccess)
            { AllowedToolNames = new HashSet<string> { "pull_request.get" } });
        var events = new List<AgentEvent>();
        await foreach (var agentEvent in runner.RunAsync(request, TestContext.Current.CancellationToken)) events.Add(agentEvent);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, item => Assert.Empty(item.Tools));
        Assert.Equal("tool_scope_denied", Assert.Single(events.OfType<AgentToolEvent>()).Result.ErrorCode);
        using var feedback = JsonDocument.Parse(Assert.Single(requests[1].Messages, message => message.Role == ModelRole.Tool).Content!);
        Assert.False(feedback.RootElement.GetProperty("IsSuccess").GetBoolean());
        Assert.NotEmpty(feedback.RootElement.GetProperty("Summary").GetString()!);
        Assert.Contains(events, agentEvent => agentEvent is AgentCompletedEvent);
        Assert.Equal(0, tool.Calls);
    }
}
