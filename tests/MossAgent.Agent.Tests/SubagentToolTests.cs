using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentToolTests
{
    [Fact]
    public async Task ExecuteAsync_UsesSelectedModelAndReturnsDialogueThroughToolPipeline()
    {
        var setup = CreateSetup([new TextDelta("Review complete.")]);
        var approval = new FixedToolApprovalService(true);
        var executor = CreateExecutor(setup.Tool, approval);
        var context = CreateContext(conversationContext: "用户：旧问题\n助手：旧答复");

        var result = await ExecuteAsync(
            executor, """{"agent":"review","task":"检查改动"}""", context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("high", setup.Provider.LastRequest!.Model.ReasoningEffort);
        Assert.Empty(setup.Provider.LastRequest.Tools);
        Assert.Contains("旧问题", setup.Provider.LastRequest.Messages[1].Content);
        Assert.Contains("检查改动", setup.Provider.LastRequest.Messages[1].Content);
        Assert.Equal(new[] { "任务", "状态", "子代理" },
            result.Dialogue!.Select(message => message.Role));
        Assert.Equal("Review complete.", result.Content);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, approval.LastDescriptor!.RiskLevel);
        Assert.Equal(ToolCapability.Network, approval.LastDescriptor.RequiredCapabilities);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidSchemaBeforeCallingModel()
    {
        var setup = CreateSetup([new TextDelta("unused")]);
        var executor = CreateExecutor(setup.Tool, new FixedToolApprovalService(true));

        var result = await ExecuteAsync(
            executor, """{"agent":"review"}""", CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotInvokeDisabledSubagent()
    {
        var setup = CreateSetup([new TextDelta("unused")], enabled: false);
        var result = await ExecuteAsync(
            CreateExecutor(setup.Tool, new FixedToolApprovalService(true)),
            """{"agent":"review","task":"检查"}""", CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal("subagent_unavailable", result.ErrorCode);
        Assert.Equal(0, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_OmitsConversationWhenSharingIsDisabled()
    {
        var setup = CreateSetup([new TextDelta("完成")], shareContext: false);
        var executor = CreateExecutor(setup.Tool, new FixedToolApprovalService(true));

        var result = await ExecuteAsync(
            executor, """{"agent":"review","task":"单独任务"}""",
            CreateContext(conversationContext: "不应传递的旧上下文"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("单独任务", setup.Provider.LastRequest!.Messages[1].Content);
        Assert.DoesNotContain("旧上下文", setup.Provider.LastRequest.Messages[1].Content);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresApprovalBeforeCallingModel()
    {
        var setup = CreateSetup([new TextDelta("unused")]);
        var executor = CreateExecutor(setup.Tool, new FixedToolApprovalService(false));

        var result = await ExecuteAsync(
            executor, """{"agent":"review","task":"检查"}""", CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal("approval_denied", result.ErrorCode);
        Assert.Equal(0, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNetworkCapabilityInReadOnlyModeBeforeApproval()
    {
        var setup = CreateSetup([new TextDelta("unused")]);
        var approval = new FixedToolApprovalService(true);
        var executor = CreateExecutor(setup.Tool, approval);

        var result = await ExecuteAsync(
            executor, """{"agent":"review","task":"检查"}""",
            CreateContext(approvalPolicy: ApprovalPolicy.ReadOnly),
            TestContext.Current.CancellationToken);

        Assert.Equal("capability_denied", result.ErrorCode);
        Assert.Null(approval.LastDescriptor);
        Assert.Equal(0, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UsesToolPipelineTimeoutAndCancellation()
    {
        var timeoutSetup = CreateSetup([new TextDelta("late")], TimeSpan.FromSeconds(1));
        var timeoutResult = await ExecuteAsync(
            CreateExecutor(timeoutSetup.Tool, new FixedToolApprovalService(true)),
            """{"agent":"review","task":"检查"}""",
            CreateContext(timeout: TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken);

        Assert.Equal("tool_timeout", timeoutResult.ErrorCode);

        var cancelSetup = CreateSetup([new TextDelta("late")], TimeSpan.FromSeconds(1));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));
        var cancelledResult = await ExecuteAsync(
            CreateExecutor(cancelSetup.Tool, new FixedToolApprovalService(true)),
            """{"agent":"review","task":"检查"}""",
            CreateContext(timeout: TimeSpan.FromSeconds(5)), cancellation.Token);

        Assert.Equal("tool_cancelled", cancelledResult.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_LimitsReportAndDialogueTogether()
    {
        var setup = CreateSetup([new TextDelta(new string('x', 500))]);
        var result = await ExecuteAsync(
            CreateExecutor(setup.Tool, new FixedToolApprovalService(true)),
            """{"agent":"review","task":"检查"}""",
            CreateContext(maximumOutputCharacters: 50),
            TestContext.Current.CancellationToken);

        Assert.True(result.OutputCharacterCount <= 50);
        Assert.NotNull(result.Dialogue);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSplitSurrogatePairWhenLimitingResponse()
    {
        var setup = CreateSetup([new TextDelta(new string('a', 24) + "😀tail")]);
        var result = await ExecuteAsync(
            CreateExecutor(setup.Tool, new FixedToolApprovalService(true)),
            """{"agent":"review","task":"检查"}""",
            CreateContext(maximumOutputCharacters: 50),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Content);
        Assert.DoesNotContain(result.Content, char.IsSurrogate);
    }

    private static (SubagentTool Tool, StubModelProvider Provider) CreateSetup(
        IReadOnlyList<ModelEvent> events,
        TimeSpan delay = default,
        bool shareContext = true,
        bool enabled = true)
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), provider.Id, "sub-model", "Sub model", 16_000, 2_000,
            "low", null, null, false, false, false, true, "{}");
        var profile = new SubagentProfile(
            Guid.NewGuid(), "review", "独立审查改动", "检查代码并报告问题。",
            provider.Id, model.Id, "high", shareContext, enabled);
        var modelProvider = new StubModelProvider(provider.Protocol, events, delay);
        var registry = new StubModelProviderRegistry(modelProvider);
        var tool = new SubagentTool(
            new InMemorySubagentConfigurationRepository([profile]),
            new InMemoryConfigurationRepository(provider, model),
            registry);
        return (tool, modelProvider);
    }

    private static ToolExecutor CreateExecutor(
        SubagentTool tool,
        FixedToolApprovalService approval) =>
        new(new ToolRegistry([tool]), approval);

    private static async Task<ToolResult> ExecuteAsync(
        ToolExecutor executor,
        string arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(arguments);
        var request = new ToolRequest(
            Guid.NewGuid().ToString("N"), SubagentTool.ToolName, document.RootElement.Clone());
        return await executor.ExecuteAsync(request, context, cancellationToken);
    }

    private static ToolExecutionContext CreateContext(
        string? conversationContext = null,
        int maximumOutputCharacters = 64 * 1024,
        TimeSpan? timeout = null,
        ApprovalPolicy approvalPolicy = ApprovalPolicy.FullAccess) =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], approvalPolicy,
            maximumOutputCharacters, timeout, ConversationContext: conversationContext);
}
