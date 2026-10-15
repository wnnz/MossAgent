using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentConfigurationBoundaryTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "child-model", "invalid value")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "child-model", "invalid value")]
    [InlineData(ProviderProtocol.AnthropicMessages, "child-model", "unexpected")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.1-pro", "xhigh")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3-pro", "medium")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-2.0-flash", "high")]
    public async Task UnsupportedSavedEffortIsRejectedBeforeAnyChildModelCall(
        ProviderProtocol protocol, string modelId, string effort)
    {
        var fixture = CreateFixture(protocol, modelId, effort);

        var result = await ExecuteAsync(fixture.Tool);

        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_reasoning_unavailable", result.ErrorCode);
        Assert.Contains("思考强度", result.Summary);
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Null(fixture.Provider.LastRequest);
        Assert.True(result.Dialogue is null || result.Dialogue.Count == 0);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "child-model", "high")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "child-model", "xhigh")]
    [InlineData(ProviderProtocol.OpenAiResponses, "child-model", "vendor-extreme_2")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "child-model", "vendor-extreme_2")]
    [InlineData(ProviderProtocol.AnthropicMessages, "child-model", "high")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.1-pro", "high")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-2.0-flash", "none")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-2.0-flash", "off")]
    public async Task SupportedSavedEffortIsUsedWithoutSubstitutingAnotherChoice(
        ProviderProtocol protocol, string modelId, string effort)
    {
        var fixture = CreateFixture(protocol, modelId, effort);

        var result = await ExecuteAsync(fixture.Tool);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(effort, fixture.Provider.LastRequest!.Model.ReasoningEffort);
        Assert.Equal(modelId, fixture.Provider.LastRequest.Model.ModelId);
        Assert.Equal(fixture.Provider.LastRequest.Provider.Id, fixture.Provider.LastRequest.Model.ProviderId);
    }

    [Fact]
    public async Task ForeignProviderModelWithMatchingIdentityCannotBeInvoked()
    {
        var fixture = CreateFixture(ProviderProtocol.OpenAiResponses, "foreign-model", "high", foreignModel: true);

        var result = await ExecuteAsync(fixture.Tool);

        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_model_unavailable", result.ErrorCode);
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Null(fixture.Provider.LastRequest);
    }

    private static (SubagentTool Tool, StubModelProvider Provider) CreateFixture(
        ProviderProtocol protocol, string modelId, string effort, bool foreignModel = false)
    {
        var provider = new AiProvider(Guid.NewGuid(), "synthetic-provider", protocol,
            new Uri("https://fixture.example.test/"), "synthetic-only", null, false, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), foreignModel ? Guid.NewGuid() : provider.Id,
            modelId, modelId, 16000, 2000, "medium", null, null, false, false, false, true, "{}");
        var profile = new SubagentProfile(Guid.NewGuid(), "review", "检查改动", "合成审查任务",
            provider.Id, model.Id, effort, false, true);
        var implementation = new StubModelProvider(protocol, [new TextDelta("完成")]);
        var tool = new SubagentTool(new InMemorySubagentConfigurationRepository([profile]),
            new InMemoryConfigurationRepository(provider, model), new StubModelProviderRegistry(implementation));
        return (tool, implementation);
    }

    private static Task<ToolResult> ExecuteAsync(SubagentTool tool)
    {
        var executor = new ToolExecutor(new ToolRegistry([tool]), new FixedToolApprovalService(true));
        var request = new ToolRequest("configured-child", SubagentTool.ToolName,
            JsonSerializer.SerializeToElement(new { agent = "review", task = "合成检查" }));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.FullAccess);
        return executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);
    }
}
