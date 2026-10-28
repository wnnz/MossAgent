using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

/// <summary>
/// Two independent tasks delegating to the same subagent at once. Same-turn tool calls are
/// sequential inside one turn, so this drives two <see cref="AgentRunner"/> streams whose
/// children must provably overlap, then checks journal row identity and per-run isolation.
/// </summary>
public sealed class SubagentConcurrentDelegationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoConcurrentDelegations_JournalDistinctRunsWithIsolatedState()
    {
        var (runner, providerRow, mainModel, journal, provider) = CreateRunner();
        var contextA = CreateContext();
        var contextB = CreateContext();
        var (eventsA, eventsB) = await RunTogetherAsync(runner,
            new AgentRunRequest(providerRow, mainModel,
                [new ModelMessage(ModelRole.User, "审查任务 A")], contextA),
            new AgentRunRequest(providerRow, mainModel,
                [new ModelMessage(ModelRole.User, "审查任务 B")], contextB));

        // Both delegations were in-flight at once (a serialized run deadlocks and times out).
        Assert.True(provider.BothStarted.Task.IsCompleted);
        Assert.Equal(2, provider.StartedChildren);

        var resultA = Assert.Single(eventsA.OfType<AgentToolEvent>()).Result;
        var resultB = Assert.Single(eventsB.OfType<AgentToolEvent>()).Result;
        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        Assert.Equal("已完成：审查任务 A", resultA.Content);
        Assert.Equal("已完成：审查任务 B", resultB.Content);
        Assert.Contains(eventsA, item => item is AgentCompletedEvent);
        Assert.Contains(eventsB, item => item is AgentCompletedEvent);
        Assert.DoesNotContain(resultA.Dialogue!, message => message.Content.Contains("审查任务 B"));
        Assert.DoesNotContain(resultB.Dialogue!, message => message.Content.Contains("审查任务 A"));

        // Journal rows: distinct run ids, own task linkage, running→completed per run.
        var beganA = Assert.Single(journal.Began, record => record.TaskId == contextA.TaskId);
        var beganB = Assert.Single(journal.Began, record => record.TaskId == contextB.TaskId);
        Assert.NotEqual(beganA.RunId, beganB.RunId);
        Assert.All(journal.Began, record =>
        {
            Assert.Equal(SubagentRunStatus.Running, record.Status);
            Assert.Equal("parent-1", record.ParentCallId);
        });
        Assert.Equal(2, journal.Completed.Count);
        Assert.Equal((beganA.RunId, SubagentRunStatus.Completed, (string?)null),
            Assert.Single(journal.Completed, entry => entry.RunId == beganA.RunId));
        Assert.Equal((beganB.RunId, SubagentRunStatus.Completed, (string?)null),
            Assert.Single(journal.Completed, entry => entry.RunId == beganB.RunId));

        // Snapshots captured each run's own task — no shared-state bleed between runs.
        using (var snapshot = JsonDocument.Parse(beganA.SnapshotJson))
            Assert.Equal("审查任务 A", snapshot.RootElement.GetProperty("taskPreview").GetString());
        using (var snapshot = JsonDocument.Parse(beganB.SnapshotJson))
            Assert.Equal("审查任务 B", snapshot.RootElement.GetProperty("taskPreview").GetString());
        Assert.Equal(beganA.RunId,
            Assert.Single(await journal.ReadTaskAsync(contextA.TaskId, Token)).RunId);
        Assert.Equal(beganB.RunId,
            Assert.Single(await journal.ReadTaskAsync(contextB.TaskId, Token)).RunId);

        // Live child progress stayed on its own task's stream.
        Assert.All(eventsA.OfType<AgentSubagentProgressEvent>(),
            item => Assert.DoesNotContain("审查任务 B", item.Message.Content));
        Assert.All(eventsB.OfType<AgentSubagentProgressEvent>(),
            item => Assert.DoesNotContain("审查任务 A", item.Message.Content));
    }

    [Fact]
    public async Task TwoConcurrentDelegations_OneFailureKeepsItsOwnTerminalState()
    {
        var (runner, providerRow, mainModel, journal, _) = CreateRunner();
        var failingTask = $"审查任务 {ConcurrentDelegationModelProvider.FailureMarker}";
        var (eventsA, eventsB) = await RunTogetherAsync(runner,
            new AgentRunRequest(providerRow, mainModel,
                [new ModelMessage(ModelRole.User, "审查任务 A")], CreateContext()),
            new AgentRunRequest(providerRow, mainModel,
                [new ModelMessage(ModelRole.User, failingTask)], CreateContext()));

        Assert.Equal("已完成：审查任务 A",
            Assert.Single(eventsA.OfType<AgentToolEvent>()).Result.Content);
        var failed = Assert.Single(eventsB.OfType<AgentToolEvent>()).Result;
        Assert.False(failed.IsSuccess);
        Assert.Equal("subagent_model_failed", failed.ErrorCode);
        Assert.Contains(eventsA, item => item is AgentCompletedEvent);
        Assert.Contains(eventsB, item => item is AgentCompletedEvent);

        // The failure terminated only its own run row; the sibling still completed.
        var terminal = journal.Completed
            .Join(journal.Began, entry => entry.RunId, record => record.RunId,
                (entry, record) => (entry.Status, entry.ErrorCode, Record: record))
            .ToArray();
        Assert.Equal(2, terminal.Length);
        var succeeded = Assert.Single(terminal, item => item.Status == SubagentRunStatus.Completed);
        var failedRow = Assert.Single(terminal, item => item.Status == SubagentRunStatus.Failed);
        Assert.Equal("subagent_model_failed", failedRow.ErrorCode);
        using (var snapshot = JsonDocument.Parse(succeeded.Record.SnapshotJson))
            Assert.Equal("审查任务 A", snapshot.RootElement.GetProperty("taskPreview").GetString());
        using (var snapshot = JsonDocument.Parse(failedRow.Record.SnapshotJson))
            Assert.Equal(failingTask, snapshot.RootElement.GetProperty("taskPreview").GetString());
    }

    private static async Task<(List<AgentEvent> A, List<AgentEvent> B)> RunTogetherAsync(
        AgentRunner runner, AgentRunRequest requestA, AgentRunRequest requestB)
    {
        var eventsA = new List<AgentEvent>();
        var eventsB = new List<AgentEvent>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        async Task CollectAsync(List<AgentEvent> events, AgentRunRequest request)
        {
            await foreach (var item in runner.RunAsync(request, cancellation.Token)) events.Add(item);
        }
        await Task.WhenAll(CollectAsync(eventsA, requestA), CollectAsync(eventsB, requestB));
        return (eventsA, eventsB);
    }

    private static (AgentRunner Runner, AiProvider ProviderRow, ModelProfile MainModel,
        RecordingSubagentRunJournal Journal, ConcurrentDelegationModelProvider Provider) CreateRunner()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture", null, false, true, new Dictionary<string, string>());
        var mainModel = CreateModel(provider.Id, "main");
        var childModel = CreateModel(provider.Id, "child");
        var profile = new SubagentProfile(Guid.NewGuid(), "review", "审查改动", "审查代码。",
            provider.Id, childModel.Id, "high", false, true);
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var modelProvider = new ConcurrentDelegationModelProvider(expectedChildren: 2);
        var models = new StubModelProviderRegistry(modelProvider);
        var subagents = new InMemorySubagentConfigurationRepository([profile]);
        var tool = new SubagentTool(subagents,
            new InMemoryConfigurationRepository(provider, childModel), models, journal);
        var registry = new ToolRegistry([tool]);
        var runner = new AgentRunner(models, registry,
            new ToolExecutor(registry, new FixedToolApprovalService(true)), subagents);
        return (runner, provider, mainModel, journal, modelProvider);
    }

    private static ToolExecutionContext CreateContext() => new(
        Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
