using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

/// <summary>
/// Workspace authorization revoked while a delegated child run is in-flight. The chain under
/// test is the real one: parent run → <see cref="SubagentTool"/> → child turn →
/// <see cref="SubagentToolGateway"/> → <see cref="ToolExecutor"/> →
/// <c>TaskWorkspaceOperation</c> → <c>ProjectWorkspaceGuard</c> →
/// <c>ProjectAuthorizationMonitor</c> lease revocation.
/// </summary>
public sealed class SubagentAuthorizationChangeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RevocationDuringInFlightChildTool_FailsRunAndJournalsTheHonestTerminalState()
    {
        await using var scope = new ProjectAuthorizationAgentScope();
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var probe = new RevocationProbeAgentTool();
        var reader = new CountingAgentTool();
        var fixture = CreateFixture(journal, scope, reader, probe, out var request);
        var events = new List<AgentEvent>();
        var running = CollectAsync(fixture.Runner, request, events);

        // Wait until the child's second tool call is genuinely executing, then revoke.
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        scope.Revoke();
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(
            () => running.WaitAsync(TimeSpan.FromSeconds(5), Token));

        // The pre-revocation call had run; the in-flight call was cancelled by the revocation.
        Assert.Equal(1, reader.Calls);
        Assert.Equal(1, probe.Calls);
        Assert.True(probe.Cancelled);

        // Journal: one running row that reached an honest failed terminal state.
        var began = Assert.Single(journal.Began);
        Assert.Equal(request.ToolContext.TaskId, began.TaskId);
        Assert.Equal("parent-1", began.ParentCallId);
        Assert.Equal(SubagentRunStatus.Running, began.Status);
        var completedEntry = Assert.Single(journal.Completed);
        Assert.Equal((began.RunId, SubagentRunStatus.Failed, (string?)"workspace_authorization_changed"),
            completedEntry);

        // Parent tool result is an honest failure attributed to the authorization change,
        // never a success, and it carries the child tool record so the trace stays explainable.
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.Contains(result.Dialogue!, message =>
            message.Role == $"工具 · {probe.Descriptor.Name}");
        Assert.DoesNotContain(events, item => item is AgentCompletedEvent);
    }

    [Fact]
    public async Task RevocationDuringInFlightChildModelTurn_CancelsRunAndJournalsTheHonestTerminalState()
    {
        await using var scope = new ProjectAuthorizationAgentScope();
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var reader = new CountingAgentTool();
        var modelProvider = new RevocationGatedModelProvider();
        var fixture = CreateFixture(journal, scope, reader, null, out var request, modelProvider);
        var events = new List<AgentEvent>();
        var running = CollectAsync(fixture.Runner, request, events);

        // Revoke while the child's second model turn is genuinely in-flight.
        await modelProvider.ChildTurn2Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        scope.Revoke();
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(
            () => running.WaitAsync(TimeSpan.FromSeconds(5), Token));

        // Only the pre-revocation tool call ran; the revocation stopped the model turn.
        Assert.Equal(1, reader.Calls);
        var began = Assert.Single(journal.Began);
        Assert.Equal(SubagentRunStatus.Running, began.Status);
        Assert.Equal((began.RunId, SubagentRunStatus.Cancelled, (string?)null),
            Assert.Single(journal.Completed));

        // The executor re-attributes the cancellation to the authorization change.
        var result = Assert.Single(events.OfType<AgentToolEvent>()).Result;
        Assert.False(result.IsSuccess);
        Assert.Equal("workspace_authorization_changed", result.ErrorCode);
        Assert.DoesNotContain(events, item => item is AgentCompletedEvent);
    }

    private static async Task CollectAsync(
        AgentRunner runner, AgentRunRequest request, List<AgentEvent> events)
    {
        await foreach (var item in runner.RunAsync(request, Token)) events.Add(item);
    }

    private static (AgentRunner Runner, AiProvider ProviderRow) CreateFixture(
        RecordingSubagentRunJournal journal,
        ProjectAuthorizationAgentScope scope,
        CountingAgentTool reader,
        RevocationProbeAgentTool? probe,
        out AgentRunRequest request,
        IModelProvider? modelProviderOverride = null)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture", null, false, true,
            new Dictionary<string, string>());
        var mainModel = CreateModel(provider.Id, "main");
        var childModel = CreateModel(provider.Id, "child");
        var profile = new SubagentProfile(Guid.NewGuid(), "review", "审查改动", "审查代码。",
            provider.Id, childModel.Id, "high", false, true);
        var childTurns = 0;
        var modelProvider = modelProviderOverride ?? new StubModelProvider(
            provider.Protocol, [], responseFactory: modelRequest =>
            {
                if (modelRequest.Model.Id == childModel.Id)
                    return ++childTurns == 1
                        ? [new ToolCallCompleted("child-1", reader.Descriptor.Name,
                            JsonSerializer.SerializeToElement(new { }))]
                        : [new ToolCallCompleted("child-2", probe!.Descriptor.Name,
                            JsonSerializer.SerializeToElement(new { }))];
                return modelRequest.Messages.Any(static message => message.Role == ModelRole.Tool)
                    ? [new TextDelta("不应到达")]
                    : [new ToolCallCompleted("parent-1", SubagentTool.ToolName,
                        JsonSerializer.SerializeToElement(new { agent = "review", task = "检查改动" }))];
            });
        var models = new StubModelProviderRegistry(modelProvider);
        var subagents = new InMemorySubagentConfigurationRepository([profile]);
        var tool = new SubagentTool(subagents,
            new InMemoryConfigurationRepository(provider, childModel), models, journal);
        var registry = probe is null
            ? new ToolRegistry([tool, reader])
            : new ToolRegistry([tool, reader, probe]);
        var runner = new AgentRunner(models, registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)), subagents);
        // The scope ships ReadOnly; subagent.run needs Network, so the run uses FullAccess.
        var context = scope.Context with { ApprovalPolicy = ApprovalPolicy.FullAccess };
        request = new AgentRunRequest(provider, mainModel,
            [new ModelMessage(ModelRole.User, "审查")], context);
        return (runner, provider);
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
