using MossAgent.Agent;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.Agent.Tests;

internal static class SubagentStreamingFixture
{
    public static (AgentRunner Runner, AgentRunRequest Request) Create(
        GatedSubagentModelProvider modelProvider, IToolExecutionAuditSink? audit = null)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture", null, false, true, new Dictionary<string, string>());
        var parent = CreateModel(provider.Id, "parent");
        var child = CreateModel(provider.Id, "child");
        var profile = new SubagentProfile(Guid.NewGuid(), "review", "检查改动", "审查代码。",
            provider.Id, child.Id, "high", true, true);
        var subagents = new InMemorySubagentConfigurationRepository([profile]);
        var models = new StubModelProviderRegistry(modelProvider);
        var tool = new SubagentTool(subagents, new InMemoryConfigurationRepository(provider, child), models);
        var registry = new ToolRegistry([tool]);
        var runner = new AgentRunner(models, registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true), audit), subagents);
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.FullAccess);
        return (runner, new AgentRunRequest(provider, parent,
            [new ModelMessage(ModelRole.User, "检查改动")], context));
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
