using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentToolInvocationTests
{
    [Fact]
    public async Task RunAsync_ExecutesChildToolThroughParentPipelineAndShowsDialogue()
    {
        var fixture = CreateFixture(approveChildTool: true);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        Assert.Equal(1, fixture.ChildTool.Calls);
        Assert.Equal(fixture.Request.ToolContext.TaskId, fixture.ChildTool.LastContext!.TaskId);
        Assert.Equal(fixture.Request.ToolContext.AuthorizedRoots,
            fixture.ChildTool.LastContext.AuthorizedRoots);
        Assert.Null(fixture.ChildTool.LastContext.SubagentTools);
        Assert.Equal(4_096, fixture.ChildTool.LastContext.MaximumOutputCharacters);
        Assert.Equal("parent-1:child-1", fixture.ChildTool.LastRequest!.CallId);

        var childRequests = fixture.Requests
            .Where(request => request.Model.Id == fixture.ChildModel.Id)
            .ToArray();
        Assert.Equal(2, childRequests.Length);
        var childRequest = childRequests[0];
        Assert.Contains(childRequest.Tools, tool => tool.Name == "test.read");
        Assert.DoesNotContain(childRequest.Tools, tool => tool.Name == SubagentTool.ToolName);

        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.True(result.IsSuccess);
        var progress = events.OfType<AgentSubagentProgressEvent>().ToArray();
        Assert.Contains(progress, item => item.Message.Role == "状态"
            && item.Message.Content.Contains("执行工具 · test.read"));
        Assert.Contains(progress, item => item.Message.Role == "工具 · test.read"
            && item.Message.Content.Contains("测试内容"));
        Assert.Equal("检视完毕", progress[^1].Message.Content);
        Assert.True(Array.FindIndex(events.ToArray(),
            item => item is AgentSubagentProgressEvent) < Array.FindIndex(
            events.ToArray(), item => item is AgentToolEvent));
        Assert.Contains(result.Dialogue!, item =>
            item.Role == "工具 · test.read" && item.Content.Contains("测试内容"));
        Assert.Contains(result.Dialogue!, item =>
            item.Role == "状态" && item.Content.Contains("第 2 轮"));
        Assert.Equal("检视完毕", result.Content);
    }

    [Fact]
    public async Task RunAsync_DeniedChildToolDoesNotExecute()
    {
        var fixture = CreateFixture(approveChildTool: false);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        Assert.Equal(0, fixture.ChildTool.Calls);
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("approval_denied", result.ErrorCode);
        Assert.Contains(result.Dialogue!, item =>
            item.Role == "工具 · test.read" && item.Content.Contains("拒绝"));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public async Task RunAsync_ChildRetainsWorkspaceGuardBeforeAndAfterItsOwnApproval(int allowedChecks, bool allowed)
    {
        var guard = new SubagentWorkspaceGuard(allowedChecks);
        var fixture = CreateFixture(approveChildTool: true);
        var request = fixture.Request with { ToolContext = fixture.Request.ToolContext with { WorkspaceGuard = guard } };
        var events = await CollectEventsAsync(fixture.Runner, request);
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? 1 : 0, fixture.ChildTool.Calls);
        Assert.Equal(Math.Min(allowedChecks + 1, 4), guard.Contexts.Count);
        Assert.All(guard.Contexts, context =>
        {
            Assert.Same(guard, context.WorkspaceGuard);
            Assert.Equal(request.ToolContext.TaskId, context.TaskId);
            Assert.Equal(request.ToolContext.WorkingDirectory, context.WorkingDirectory);
            Assert.Equal(request.ToolContext.AuthorizedRoots, context.AuthorizedRoots);
        });
        Assert.Contains(guard.Contexts, context => context.MaximumOutputCharacters == 4096 && context.SubagentTools is null);
        Assert.Equal(allowed ? 2 : 1, fixture.Requests.Count(item => item.Model.Id == fixture.ChildModel.Id));
        if (!allowed) Assert.Equal("workspace_denied", result.ErrorCode);
        Assert.All(result.Dialogue!, message => Assert.DoesNotContain("synthetic-workspace-secret", message.Content));
    }

    [Fact]
    public async Task RunAsync_ChildCannotRecursivelyDelegate()
    {
        var fixture = CreateFixture(
            approveChildTool: true, requestedToolName: SubagentTool.ToolName);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        Assert.Equal(0, fixture.ChildTool.Calls);
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_tool_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task RunAsync_ChildReceivesSharedWindowImagesWhenCapable()
    {
        var image = ModelImage.FromBytes("image/png", new byte[64]);
        var fixture = CreateFixture(approveChildTool: true, childSupportsImages: true,
            messages:
            [
                new ModelMessage(ModelRole.User, "共享图", Images: [image]),
                new ModelMessage(ModelRole.User, "最新请求")
            ]);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        var childRequest = fixture.Requests.First(request => request.Model.Id == fixture.ChildModel.Id);
        var user = childRequest.Messages[1];
        var attached = Assert.Single(user.Images);
        Assert.Equal(image.MediaType, attached.MediaType);
        Assert.Equal(image.ByteCount, attached.ByteCount);
        Assert.Equal(image.Base64Data, attached.Base64Data);
        Assert.Contains("共享图", user.Content);
        Assert.Contains("[包含 1 张图像输入]", user.Content);
        Assert.DoesNotContain(image.Base64Data, user.Content);
        Assert.True(Assert.Single(events.OfType<AgentToolEvent>()).Result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_ChildReceivesNoSharedTextOrImagesWhenSharingIsOff()
    {
        var image = ModelImage.FromBytes("image/png", new byte[64]);
        var fixture = CreateFixture(approveChildTool: true, shareContext: false,
            childSupportsImages: true,
            messages: [new ModelMessage(ModelRole.User, "私有图", Images: [image])]);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        var childRequest = fixture.Requests.First(request => request.Model.Id == fixture.ChildModel.Id);
        var user = childRequest.Messages[1];
        Assert.True(user.Images.IsDefaultOrEmpty);
        Assert.Equal("检查当前改动", user.Content);
        Assert.DoesNotContain("私有图", user.Content);
        Assert.True(Assert.Single(events.OfType<AgentToolEvent>()).Result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_ChildRunFailsBeforeModelCallWhenImagesUnsupported()
    {
        var image = ModelImage.FromBytes("image/png", new byte[64]);
        var fixture = CreateFixture(approveChildTool: true, childSupportsImages: false,
            messages: [new ModelMessage(ModelRole.User, "共享图", Images: [image])]);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);

        Assert.DoesNotContain(fixture.Requests, request => request.Model.Id == fixture.ChildModel.Id);
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_images_unsupported", result.ErrorCode);
    }

    [Fact]
    public async Task ChildSignedContinuation_StaysInChildRequestsAndNeverEntersDialogue()
    {
        var fixture = CreateFixture(approveChildTool: true, signed: true);
        var events = await CollectEventsAsync(fixture.Runner, fixture.Request);
        var childRequests = fixture.Requests.Where(request => request.Model.Id == fixture.ChildModel.Id).ToArray();
        var assistant = Assert.Single(childRequests[1].Messages, message => message.Role == ModelRole.Assistant);

        Assert.NotNull(assistant.Continuation);
        Assert.Equal(fixture.ChildModel.ModelId, assistant.Continuation.ModelId);
        Assert.Equal("native-child", Assert.Single(assistant.ToolCalls!).ProviderCallId);
        Assert.All(fixture.Requests.Where(request => request.Model.Id != fixture.ChildModel.Id), request =>
            Assert.All(request.Messages, message => Assert.Null(message.Continuation)));
        Assert.Equal("parent-1:child-1", fixture.ChildTool.LastRequest!.CallId);
        Assert.All(Assert.Single(events.OfType<AgentToolEvent>()).Result.Dialogue!, message =>
            Assert.DoesNotContain("opaque-child", message.Content));
    }

    private static async Task<IReadOnlyList<AgentEvent>> CollectEventsAsync(
        AgentRunner runner,
        AgentRunRequest request)
    {
        var events = new List<AgentEvent>();
        await foreach (var agentEvent in runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            events.Add(agentEvent);
        return events;
    }

    private static (
        AgentRunner Runner,
        AgentRunRequest Request,
        ModelProfile ChildModel,
        CountingAgentTool ChildTool,
        List<ModelRequest> Requests) CreateFixture(
        bool approveChildTool,
        string requestedToolName = "test.read",
        bool signed = false,
        bool shareContext = true,
        bool childSupportsImages = false,
        IReadOnlyList<ModelMessage>? messages = null)
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "test", signed ? ProviderProtocol.GeminiGenerateContent : ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());
        var hasImages = messages?.Any(static message => !message.Images.IsDefaultOrEmpty) == true;
        var mainModel = CreateModel(provider.Id, "main", hasImages);
        var childModel = CreateModel(provider.Id, signed ? "gemini-3-flash-preview" : "child", childSupportsImages);
        var profile = new SubagentProfile(
            Guid.NewGuid(), "review", "检查改动", "审查代码。",
            provider.Id, childModel.Id, "high", shareContext, true);
        var requests = new List<ModelRequest>();
        var parentTurns = 0;
        var childTurns = 0;
        Func<ModelRequest, IReadOnlyList<ModelEvent>> responses = request =>
        {
            requests.Add(request);
            if (request.Model.Id == childModel.Id)
            {
                childTurns++;
                if (signed && childTurns == 1)
                    return [new ToolCallCompleted("child-1", requestedToolName, JsonSerializer.SerializeToElement(new { }), "native-child"),
                        new ModelCompleted(Continuation: new ModelContinuation(provider.Id, provider.Protocol, childModel.ModelId,
                            JsonSerializer.SerializeToElement(new { parts = new[] { new { thoughtSignature = "opaque-child" } } })))];
                return childTurns == 1
                    ? [new ToolCallCompleted("child-1", requestedToolName,
                        JsonSerializer.SerializeToElement(new { }))]
                    : [new TextDelta("检视完毕")];
            }

            parentTurns++;
            return parentTurns == 1
                ? [new ToolCallCompleted("parent-1", SubagentTool.ToolName,
                    JsonSerializer.SerializeToElement(
                        new { agent = "review", task = "检查当前改动" }))]
                : [new TextDelta("汇总完成")];
        };
        var modelProvider = new StubModelProvider(provider.Protocol, [],
            responseFactory: responses);
        var modelRegistry = new StubModelProviderRegistry(modelProvider);
        var subagents = new InMemorySubagentConfigurationRepository([profile]);
        var subagentTool = new SubagentTool(subagents,
            new InMemoryConfigurationRepository(provider, childModel), modelRegistry);
        var childTool = new CountingAgentTool();
        var registry = new ToolRegistry([subagentTool, childTool]);
        var executor = new ToolExecutor(registry,
            new SelectiveToolApprovalService(descriptor =>
                descriptor.Name == SubagentTool.ToolName || approveChildTool));
        var runner = new AgentRunner(modelRegistry, registry, executor, subagents);
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.AskEveryTime);
        var request = new AgentRunRequest(provider, mainModel,
            messages ?? [new ModelMessage(ModelRole.User, "审查代码")], context);
        return (runner, request, childModel, childTool, requests);
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId, bool supportsImages = false) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, supportsImages, false, false, true, "{}");
}
