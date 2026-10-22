using System.Text;
using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentRunnerSubagentTests
{
    [Fact]
    public async Task RunAsync_PassesOnlyConversationTextToSharedSubagent()
    {
        var fixture = CreateSharedContextFixture();
        var events = new List<AgentEvent>();

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           fixture.Request, TestContext.Current.CancellationToken))
            events.Add(agentEvent);

        var childRequest = Assert.Single(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id);
        var sharedContext = childRequest.Messages[1].Content;
        Assert.Contains("旧问题", sharedContext);
        Assert.Contains("读取项目说明", sharedContext);
        Assert.Contains("当前问题", sharedContext);
        Assert.Contains("检查当前改动", sharedContext);
        Assert.DoesNotContain("工具输出不应共享", sharedContext);
        Assert.Empty(childRequest.Tools);

        var toolEvent = Assert.Single(events.OfType<AgentToolEvent>());
        Assert.Equal(SubagentTool.ToolName, toolEvent.ToolName);
        Assert.Collection(
            toolEvent.Result.Dialogue!,
            message =>
            {
                Assert.Equal("任务", message.Role);
                Assert.Equal("检查当前改动", message.Content);
            },
            message =>
            {
                Assert.Equal("状态", message.Role);
                Assert.Contains("第 1 轮", message.Content);
            },
            message =>
            {
                Assert.Equal("子代理", message.Role);
                Assert.Equal("检查完成", message.Content);
            });
    }

    [Fact]
    public async Task RunAsync_DoesNotSplitSurrogatePairWhenTruncatingSharedContext()
    {
        var fixture = CreateSharedContextFixture();
        const string marker = "[较早上下文已省略]\n";
        const int maximumCharacters = 12_000;
        var suffixLength = maximumCharacters - marker.Length - 1;
        var content = string.Concat(
            new string('a', 100), "😀",
            new string('b', suffixLength - Environment.NewLine.Length));
        var request = fixture.Request with
        {
            Messages = [new ModelMessage(ModelRole.User, content)]
        };

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            Assert.NotNull(agentEvent);

        var childContext = Assert.Single(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id).Messages[1].Content;
        var markerIndex = childContext.IndexOf(marker, StringComparison.Ordinal);
        Assert.NotEqual(-1, markerIndex);
        Assert.False(char.IsLowSurrogate(childContext[markerIndex + marker.Length]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_SharesParentImagesOnlyWhenContextSharingIsEnabled(bool shareContext)
    {
        var fixture = CreateSharedContextFixture(shareContext);
        var image = ModelImage.FromBytes("image/png", Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"));
        var request = fixture.Request with
        {
            Messages = fixture.Request.Messages.Take(fixture.Request.Messages.Count - 1)
                .Append(new ModelMessage(ModelRole.User, "当前问题", Images: [image]))
                .ToArray()
        };

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            Assert.NotNull(agentEvent);

        var childRequest = Assert.Single(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id);
        var childUser = Assert.Single(childRequest.Messages,
            item => item.Role == ModelRole.User);
        if (shareContext)
        {
            var sharedImage = Assert.Single(childUser.Images);
            Assert.Equal(image.MediaType, sharedImage.MediaType);
            Assert.Equal(image.Base64Data, sharedImage.Base64Data);
        }
        else
        {
            Assert.True(childUser.Images.IsDefaultOrEmpty);
            Assert.DoesNotContain(image.Base64Data, childRequest.Messages[1].Content);
        }
    }

    [Fact]
    public async Task RunAsync_FailsExplicitlyWhenSharedChildModelDoesNotSupportImages()
    {
        var fixture = CreateSharedContextFixture(
            shareContext: true, childSupportsImages: false);
        var image = ModelImage.FromBytes("image/png",
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"));
        var request = fixture.Request with
        {
            Messages =
            [
                new ModelMessage(ModelRole.User, "分析图片", Images: [image])
            ]
        };
        var events = new List<AgentEvent>();

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            events.Add(agentEvent);

        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_images_unsupported", result.ErrorCode);
        Assert.DoesNotContain(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_SharesParentPdfInputsOnlyWhenContextSharingIsEnabled(bool shareContext)
    {
        var fixture = CreateSharedContextFixture(
            shareContext, childSupportsImages: true, childSupportsFiles: true);
        var file = PdfFile();
        var request = fixture.Request with
        {
            Messages =
            [
                fixture.Request.Messages[0],
                new ModelMessage(ModelRole.User, "分析 PDF", Files: [file])
            ]
        };

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            Assert.NotNull(agentEvent);

        var childRequest = Assert.Single(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id);
        var childUser = Assert.Single(childRequest.Messages,
            item => item.Role == ModelRole.User);
        if (shareContext)
        {
            var shared = Assert.Single(childUser.Files);
            Assert.Equal(file.FileName, shared.FileName);
            Assert.Equal(file.Base64Data, shared.Base64Data);
            Assert.Contains("PDF inputs are attached separately", childUser.Content);
        }
        else
        {
            Assert.True(childUser.Files.IsDefaultOrEmpty);
            Assert.DoesNotContain(file.Base64Data, childUser.Content);
        }
    }

    [Fact]
    public async Task RunAsync_FailsExplicitlyWhenSharedChildModelDoesNotSupportPdf()
    {
        var fixture = CreateSharedContextFixture(
            shareContext: true, childSupportsImages: true, childSupportsFiles: false);
        var request = fixture.Request with
        {
            Messages = [new ModelMessage(ModelRole.User, "分析 PDF", Files: [PdfFile()])]
        };
        var events = new List<AgentEvent>();

        await foreach (var agentEvent in fixture.Runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            events.Add(agentEvent);

        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_files_unsupported", result.ErrorCode);
        Assert.DoesNotContain(fixture.Requests,
            item => item.Model.Id == fixture.ChildModel.Id);
    }

    private static (
        AgentRunner Runner,
        List<ModelRequest> Requests,
        ModelProfile ChildModel,
        AgentRunRequest Request) CreateSharedContextFixture(
        bool shareContext = true, bool childSupportsImages = true, bool childSupportsFiles = false)
    {
        var provider = CreateProvider();
        var mainModel = CreateModel(provider.Id, "main", supportsFiles: true);
        var childModel = CreateModel(
            provider.Id, "child", childSupportsImages, childSupportsFiles);
        var profile = new SubagentProfile(
            Guid.NewGuid(), "review", "独立审查", "检查改动。",
            provider.Id, childModel.Id, "high", shareContext, true);
        var requests = new List<ModelRequest>();
        var parentCalls = 0;
        Func<ModelRequest, IReadOnlyList<ModelEvent>> responseFactory = modelRequest =>
        {
            requests.Add(modelRequest);
            if (modelRequest.Model.Id == childModel.Id)
                return [new TextDelta("检查完成")];
            parentCalls++;
            return parentCalls == 1
                ? [new ToolCallCompleted("call-1", SubagentTool.ToolName,
                    JsonSerializer.SerializeToElement(new { agent = "review", task = "检查当前改动" }))]
                : [new TextDelta("已汇总")];
        };
        var modelProvider = new StubModelProvider(provider.Protocol, [], responseFactory: responseFactory);
        var configurations = new InMemoryConfigurationRepository(provider, childModel);
        var subagents = new InMemorySubagentConfigurationRepository([profile]);
        var tool = new SubagentTool(subagents, configurations,
            new StubModelProviderRegistry(modelProvider));
        var registry = new ToolRegistry([tool]);
        var runner = new AgentRunner(new StubModelProviderRegistry(modelProvider), registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)), subagents);
        var priorCall = new ModelToolCall("prior-call", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" }));
        var messages = new List<ModelMessage>
        {
            new(ModelRole.User, "旧问题"),
            new(ModelRole.Assistant, "读取项目说明", ToolCalls: [priorCall]),
            new(ModelRole.Tool, "工具输出不应共享", "prior-call", "filesystem.read_file"),
            new(ModelRole.User, "当前问题")
        };
        var request = new AgentRunRequest(provider, mainModel, messages,
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
                ApprovalPolicy.FullAccess));
        return (runner, requests, childModel, request);
    }

    [Fact]
    public async Task RunAsync_OnlyOffersEnabledSubagentsAndDescribesTheirTriggerConditions()
    {
        var provider = CreateProvider();
        var mainModel = CreateModel(provider.Id, "main");
        var childModel = CreateModel(provider.Id, "child");
        var subagentProfile = new SubagentProfile(
            Guid.NewGuid(), "review", "独立审查较大的代码改动。", "审查代码。",
            provider.Id, childModel.Id, "high", false, true);
        var configurationRepository = new InMemoryConfigurationRepository(provider, childModel);
        var modelProvider = new StubModelProvider(provider.Protocol, [new TextDelta("完成")]);
        var tool = new SubagentTool(
            new InMemorySubagentConfigurationRepository([subagentProfile]),
            configurationRepository,
            new StubModelProviderRegistry(modelProvider));
        var registry = new ToolRegistry([tool]);
        var runner = new AgentRunner(
            new StubModelProviderRegistry(modelProvider), registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)),
            new InMemorySubagentConfigurationRepository([subagentProfile]));
        var request = new AgentRunRequest(
            provider, mainModel, [new ModelMessage(ModelRole.User, "总结这项改动")],
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(),
                [Path.GetTempPath()], ApprovalPolicy.FullAccess));

        await foreach (var agentEvent in runner.RunAsync(request, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(agentEvent);
        }

        Assert.Contains(modelProvider.LastRequest!.Tools, descriptor =>
            descriptor.Name == SubagentTool.ToolName);
        Assert.Contains("独立审查较大的代码改动", modelProvider.LastRequest.Messages[0].Content);

        var disabledRepository = new InMemorySubagentConfigurationRepository(
            [subagentProfile with { IsEnabled = false }]);
        var disabledProvider = new StubModelProvider(provider.Protocol, [new TextDelta("完成")]);
        var disabledRunner = new AgentRunner(
            new StubModelProviderRegistry(disabledProvider), registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)), disabledRepository);

        await foreach (var disabledEvent in disabledRunner.RunAsync(
                           request, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(disabledEvent);
        }

        Assert.DoesNotContain(disabledProvider.LastRequest!.Tools, descriptor =>
            descriptor.Name == SubagentTool.ToolName);
    }

    [Fact]
    public async Task RunAsync_UsesSelectedAgentInstructionsWithoutReplacingSafetyRules()
    {
        var provider = CreateProvider();
        var model = CreateModel(provider.Id, "main");
        var modelProvider = new StubModelProvider(provider.Protocol, [new TextDelta("完成")]);
        var registry = new ToolRegistry([]);
        var runner = new AgentRunner(
            new StubModelProviderRegistry(modelProvider), registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)),
            new InMemorySubagentConfigurationRepository([]));
        var request = new AgentRunRequest(provider, model,
            [new ModelMessage(ModelRole.User, "检查代码")],
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(),
                [Path.GetTempPath()], ApprovalPolicy.ReadOnly),
            AgentInstructions: "优先检查线程安全。");

        await foreach (var agentEvent in runner.RunAsync(
                           request, TestContext.Current.CancellationToken))
            Assert.NotNull(agentEvent);

        var prompt = modelProvider.LastRequest!.Messages[0].Content;
        Assert.Contains("优先检查线程安全", prompt);
        Assert.Contains("只操作授权目录", prompt);
        Assert.Contains("ReadOnly", prompt);
    }

    private static AiProvider CreateProvider() =>
        new(Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(
        Guid providerId, string modelId, bool supportsImages = true, bool supportsFiles = false) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, supportsImages, supportsFiles, false, true, "{}");

    private static ModelFile PdfFile() =>
        ModelFile.FromAttachment("shared.pdf", AttachmentDocument.FromDecodedBytes(
            AttachmentDocument.PdfMediaType,
            Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n")));
}
