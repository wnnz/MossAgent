using MossAgent.Application.Agent;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentSteeringBoundaryTests
{
    [Fact]
    public void EveryRepeatedRequiredInputMustFitAndSmallRequiredInputSurvivesOtherContentTrimming()
    {
        var taskId = Guid.NewGuid();
        var (_, request, _) = Create(taskId, new SteeringProbeModelProvider(), new ProbeAgentSteeringInput(taskId), false);
        var model = request.Model with { ContextLength = 2048, MaximumOutputTokens = 1024 };
        var first = new ModelMessage(ModelRole.User, new string('g', 1500));
        var second = new ModelMessage(ModelRole.User, first.Content);
        Assert.Throws<PinnedModelContextException>(() => ModelContextWindowBuilder.Build([first, second], [], model, null, [first, second]));
        var small = new ModelMessage(ModelRole.User, "retain this guidance completely");
        var result = ModelContextWindowBuilder.Build([small, new(ModelRole.Assistant, new string('a', 12000))], [], model, null, [small]);
        Assert.Equal(small.Content, result[0].Content);
        Assert.True(result[1].Content.Length < 12000);
    }

    [Fact]
    public async Task GuidanceDuringAPlainStreamContinuesTheSameRunWithoutReplayingTheFirstRequest()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept("new guidance"); } };
        var (runner, request, tool) = Create(taskId, provider, input, false);
        var events = await RunAsync(runner, request);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(0, tool.Calls);
        Assert.DoesNotContain(provider.Requests[0].Messages, message => message.Content == "new guidance");
        var later = provider.Requests[1].Messages;
        Assert.Equal(new[] { "initial", "answer 1", "new guidance" }, later.Where(message => message.Role != ModelRole.System).Select(message => message.Content));
        var applied = Assert.Single(events.OfType<AgentSteeringAppliedEvent>());
        Assert.Equal(input.RunId, applied.Application.Run.Id);
        Assert.Equal("answer 1", applied.Application.PrecedingAssistant!.Content);
        Assert.Single(events.OfType<AgentCompletedEvent>());
        Assert.True(input.Closed);
        Assert.All(provider.Requests, item => { Assert.Equal(request.Provider, item.Provider); Assert.Equal(request.Model, item.Model); Assert.Empty(item.Tools); });
    }

    [Fact]
    public async Task InputDuringToolsWaitsForEveryResultAndDoesNotRepeatEitherCall()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var provider = new SteeringProbeModelProvider { FirstTurnTools = true };
        var (runner, request, tool) = Create(taskId, provider, input, true);
        var events = new List<AgentEvent>();
        await foreach (var item in runner.RunAsync(request, TestContext.Current.CancellationToken))
        {
            events.Add(item);
            if (item is AgentToolEvent completed && completed.CallId == "first-call") input.Accept("after all tool results");
        }
        Assert.Equal(2, tool.Calls);
        Assert.Equal(2, provider.Requests.Count);
        var messages = provider.Requests[1].Messages.Where(message => message.Role != ModelRole.System).ToArray();
        Assert.Equal(new[] { ModelRole.User, ModelRole.Assistant, ModelRole.Tool, ModelRole.Tool, ModelRole.User }, messages.Select(message => message.Role));
        Assert.Equal(new[] { "first-call", "second-call" }, messages.Where(message => message.Role == ModelRole.Tool).Select(message => message.ToolCallId));
        Assert.Equal("after all tool results", messages[^1].Content);
        Assert.Null(Assert.Single(events.OfType<AgentSteeringAppliedEvent>()).Application.PrecedingAssistant);
        Assert.All(events.OfType<AgentToolEvent>(), item => Assert.True(item.Result.IsSuccess));
        Assert.Equal(taskId, tool.LastContext!.TaskId);
    }

    [Fact]
    public async Task OversizedGuidanceRemainsPendingWithoutAClippedSecondRequest()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept(new string('界', 32000)); } };
        var (runner, request, _) = Create(taskId, provider, input, false);
        request = request with { Model = request.Model with { ContextLength = 2048 } };
        await Assert.ThrowsAsync<PinnedModelContextException>(() => RunAsync(runner, request));
        Assert.Single(provider.Requests);
        Assert.Equal(1, input.PendingCount);
        Assert.Empty(input.Applications);
    }

    [Fact]
    public async Task AnotherTasksMailboxIsRefusedBeforeAnyModelOrToolExecution()
    {
        var taskId = Guid.NewGuid();
        var provider = new SteeringProbeModelProvider();
        var (runner, request, tool) = Create(taskId, provider, new ProbeAgentSteeringInput(Guid.NewGuid()), true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(runner, request));
        Assert.Empty(provider.Requests);
        Assert.Equal(0, tool.Calls);
    }

    [Fact]
    public async Task AttachmentContentsReachTheModelAsContextTextAndNativeImages()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var textAttachment = new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "notes.txt", 9, new string('a', 64));
        var imageAttachment = new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "shot.png", 8, new string('b', 64), "image/png");
        var store = new ProbeConversationAttachmentStore
        {
            Resolve = attachments => attachments.Select(item => item.MediaType == "image/png"
                ? new WorkspaceAttachmentContent("src/shot.png", item.DisplayPath, string.Empty, item.ByteCount,
                    AttachmentImage.FromDecodedBytes("image/png", new byte[8], 1, 1))
                : new WorkspaceAttachmentContent("src/notes.txt", item.DisplayPath, "file body", item.ByteCount)).ToArray()
        };
        var provider = new SteeringProbeModelProvider
        {
            DuringStream = turn =>
            {
                if (turn == 1) { input.Accept("guidance", textAttachment); input.Accept("with image", imageAttachment); }
            }
        };
        var (runner, request, _) = Create(taskId, provider, input, false);
        request = request with { Model = request.Model with { SupportsImages = true }, AttachmentStore = store };
        var events = await RunAsync(runner, request);
        var later = provider.Requests[1].Messages.Where(message => message.Role != ModelRole.System).ToArray();
        var steered = later.Where(message => message.Role == ModelRole.User).Skip(1).ToArray();
        Assert.Equal(2, steered.Length);
        Assert.StartsWith("guidance", steered[0].Content);
        Assert.Contains("<workspace_attachments>", steered[0].Content);
        Assert.Contains("file body", steered[0].Content);
        Assert.True(steered[0].Images.IsDefaultOrEmpty);
        Assert.Equal("with image", steered[1].Content);
        var image = Assert.Single(steered[1].Images);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(8, image.ByteCount);
        Assert.Single(events.OfType<AgentSteeringAppliedEvent>());
        Assert.Equal(2, store.Reads.Count);
        Assert.All(store.Reads, read => Assert.Equal(taskId, read.TaskId));
    }

    [Fact]
    public async Task ImageAttachmentsWithoutModelCapabilityStayPendingAndFailTheBoundary()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var image = new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "shot.png", 8, new string('b', 64), "image/png");
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept("with image", image); } };
        var (runner, request, _) = Create(taskId, provider, input, false);
        var failure = await Assert.ThrowsAsync<ModelCapabilityException>(() => RunAsync(runner, request));
        Assert.Equal(ModelCapabilityException.UnsupportedImageInput, failure.Code);
        Assert.Single(provider.Requests);
        Assert.Equal(1, input.PendingCount);
        Assert.Empty(input.Applications);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("bytes")]
    public async Task ImageLimitViolationsStayPendingAndFailTheBoundary(string kind)
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var attachments = kind == "count"
            ? Enumerable.Range(0, ModelImageRequestValidation.MaximumImages + 1).Select(_ =>
                new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "shot.png", 8, new string('b', 64), "image/png")).ToArray()
            : [new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "shot.png",
                AttachmentImage.MaximumTotalByteCount + 1, new string('b', 64), "image/png")];
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept("with image", attachments); } };
        var (runner, request, _) = Create(taskId, provider, input, false);
        request = request with { Model = request.Model with { SupportsImages = true }, AttachmentStore = new ProbeConversationAttachmentStore() };
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => RunAsync(runner, request));
        Assert.IsType<ArgumentException>(failure);
        Assert.Equal(1, input.PendingCount);
        Assert.Empty(input.Applications);
    }

    [Fact]
    public async Task AttachmentCostsCountAgainstTheContextBudgetBeforeApplying()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var attachment = new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "large.txt", 30000, new string('a', 64));
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept("guidance", attachment); } };
        var (runner, request, _) = Create(taskId, provider, input, false);
        request = request with { Model = request.Model with { ContextLength = 2048 }, AttachmentStore = new ProbeConversationAttachmentStore() };
        await Assert.ThrowsAsync<PinnedModelContextException>(() => RunAsync(runner, request));
        Assert.Single(provider.Requests);
        Assert.Equal(1, input.PendingCount);
        Assert.Empty(input.Applications);
    }

    [Fact]
    public async Task AttachmentSnapshotReadFailureSurfacesAsUnavailableAfterApplication()
    {
        var taskId = Guid.NewGuid();
        var input = new ProbeAgentSteeringInput(taskId);
        var attachment = new ConversationAttachment(Guid.NewGuid(), taskId, Guid.Empty, "notes.txt", 9, new string('a', 64));
        var store = new ProbeConversationAttachmentStore { ReadFailure = new IOException("missing snapshot") };
        var provider = new SteeringProbeModelProvider { DuringStream = turn => { if (turn == 1) input.Accept("guidance", attachment); } };
        var (runner, request, _) = Create(taskId, provider, input, false);
        request = request with { AttachmentStore = store };
        await Assert.ThrowsAsync<ConversationAttachmentUnavailableException>(() => RunAsync(runner, request));
        Assert.Single(input.Applications);
    }

    private static (AgentRunner Runner, AgentRunRequest Request, CountingAgentTool Tool) Create(Guid taskId,
        SteeringProbeModelProvider stream, IAgentSteeringInput input, bool toolsEnabled)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", stream.Protocol, new Uri("https://fixture.invalid/"),
            "synthetic", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 32000, 1024,
            "medium", null, null, false, false, true, true, "{}");
        var tool = new CountingAgentTool();
        var registry = new ToolRegistry([tool]);
        var runner = new AgentRunner(new StubModelProviderRegistry(stream), registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)), new InMemorySubagentConfigurationRepository([]));
        var context = new ToolExecutionContext(taskId, toolsEnabled ? Path.GetTempPath() : string.Empty,
            toolsEnabled ? [Path.GetTempPath()] : [], ApprovalPolicy.ReadOnly) { ToolsEnabled = toolsEnabled };
        return (runner, new(provider, model, [new(ModelRole.User, "initial")], context, Steering: input), tool);
    }

    private static async Task<IReadOnlyList<AgentEvent>> RunAsync(AgentRunner runner, AgentRunRequest request)
    {
        var events = new List<AgentEvent>();
        await foreach (var item in runner.RunAsync(request, TestContext.Current.CancellationToken)) events.Add(item);
        return events;
    }
}
