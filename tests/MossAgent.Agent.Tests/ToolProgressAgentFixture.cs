using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.Agent.Tests;

internal static class ToolProgressAgentFixture
{
    public static (AgentRunner Runner, AgentRunRequest Request) Create(IToolExecutor? executor = null, ToolExecutionContext? context = null)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "synthetic", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "synthetic", "synthetic", 32000, 1024,
            "off", null, null, false, false, true, true, "{}");
        var turn = 0;
        var modelProvider = new StubModelProvider(provider.Protocol, [], responseFactory: _ => ++turn == 1
            ? [new ToolCallCompleted("progress-call", "test.read", JsonSerializer.SerializeToElement(new { credential = "synthetic-argument-secret" })), new ModelCompleted()]
            : [new TextDelta("done"), new ModelCompleted()]);
        var registry = new ToolRegistry([new CountingAgentTool()]);
        var runner = new AgentRunner(new StubModelProviderRegistry(modelProvider), registry,
            executor ?? new ToolExecutor(registry, new FixedToolApprovalService(true)), new InMemorySubagentConfigurationRepository([]));
        context ??= new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);
        return (runner, new AgentRunRequest(provider, model, [new ModelMessage(ModelRole.User, "read")], context));
    }
}
