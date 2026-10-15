using System.Text.Json;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ProjectlessChatAgentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlainChat_AdvertisesNoToolsAndNeverExecutesUnsolicitedCalls(bool unsolicitedCall)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.invalid/"), "synthetic", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 16000, 1024,
            "medium", null, null, false, false, true, true, "{}");
        var catalog = new DescriptorProbeToolCatalog([
            new("file.read", "read", "{}", ToolRiskLevel.ReadOnly, ToolCapability.FileRead),
            new(SubagentTool.ToolName, "delegate", "{}", ToolRiskLevel.Mutation, ToolCapability.FileWrite)]);
        var executed = false;
        var executor = new ToolProgressProbeExecutor((_, _, _) =>
        {
            executed = true;
            throw new InvalidOperationException("Must not execute.");
        });
        var child = new SubagentProfile(Guid.NewGuid(), "child", "trigger", "instructions", provider.Id,
            model.Id, "medium", false, true);
        var stream = new StubModelProvider(provider.Protocol, unsolicitedCall
            ? [new ToolCallCompleted("call", "file.read", JsonSerializer.SerializeToElement(new { }))]
            : [new TextDelta("普通聊天回复")]);
        var runner = new AgentRunner(new StubModelProviderRegistry(stream), catalog, executor,
            new InMemorySubagentConfigurationRepository([child]));
        var context = new ToolExecutionContext(Guid.NewGuid(), string.Empty, [], ApprovalPolicy.FullAccess) { ToolsEnabled = false };
        var request = new AgentRunRequest(provider, model, [new(ModelRole.User, "hello")], context);
        var events = new List<AgentEvent>();
        await foreach (var item in runner.RunAsync(request, TestContext.Current.CancellationToken)) events.Add(item);

        Assert.Empty(stream.LastRequest!.Tools);
        Assert.Equal(0, catalog.Reads);
        Assert.False(executed);
        Assert.Equal(1, stream.Calls);
        Assert.Contains("无项目", stream.LastRequest.Messages[0].Content);
        Assert.DoesNotContain("child", stream.LastRequest.Messages[0].Content);
        Assert.Equal(unsolicitedCall, events.OfType<AgentFailureEvent>().Any());
        Assert.Equal(!unsolicitedCall, events.OfType<AgentCompletedEvent>().Any());
        if (unsolicitedCall) Assert.Equal("tools_disabled", Assert.Single(events.OfType<AgentFailureEvent>()).Code);
    }
}
