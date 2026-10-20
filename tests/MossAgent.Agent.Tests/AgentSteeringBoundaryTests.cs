using MossAgent.Application.Agent;
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
