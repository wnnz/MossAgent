using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentToolJournalTests
{
    [Fact]
    public async Task ExecuteAsync_JournalsSuccessfulRunWithResolvedSnapshot()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var setup = CreateSetup([new TextDelta("完成")], journal);
        var context = CreateContext() with
        {
            SubagentImages = [new SubagentSharedImage("image/png", 4, "AQIDBA==")]
        };

        var result = await ExecuteAsync(setup.Tool, context, "parent-call-1",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var began = Assert.Single(journal.Began);
        Assert.Equal(context.TaskId, began.TaskId);
        Assert.Equal("parent-call-1", began.ParentCallId);
        Assert.Equal(SubagentRunStatus.Running, began.Status);
        Assert.Equal("review", began.ProfileName);
        Assert.Null(began.ErrorCode);
        Assert.True(began.CreatedAt <= began.UpdatedAt);
        Assert.True(began.SnapshotJson.Length <= 16_384);
        var completed = Assert.Single(journal.Completed);
        Assert.Equal((began.RunId, SubagentRunStatus.Completed, (string?)null), completed);

        using var snapshot = JsonDocument.Parse(began.SnapshotJson);
        var root = snapshot.RootElement;
        Assert.Equal("review", root.GetProperty("profile").GetString());
        Assert.Equal(setup.Profile.SystemPrompt.Length, root.GetProperty("systemPromptLength").GetInt32());
        Assert.Equal(setup.ProviderRow.Id.ToString("D"), root.GetProperty("providerId").GetString());
        Assert.Equal(nameof(ProviderProtocol.OpenAiResponses), root.GetProperty("providerProtocol").GetString());
        Assert.Equal(setup.Model.Id.ToString("D"), root.GetProperty("modelProfileId").GetString());
        Assert.Equal("sub-model", root.GetProperty("modelId").GetString());
        Assert.Equal("high", root.GetProperty("reasoningEffort").GetString());
        Assert.True(root.GetProperty("shareContext").GetBoolean());
        Assert.Equal("检查改动", root.GetProperty("taskPreview").GetString());
        Assert.Equal(4, root.GetProperty("taskLength").GetInt32());
        Assert.Equal(1, root.GetProperty("sharedImageCount").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_JournalsFailedRunWithErrorCode()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var setup = CreateSetup([new ModelFailed("provider_error", "synthetic")], journal);

        var result = await ExecuteAsync(setup.Tool, CreateContext(), "parent-call-2",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        var began = Assert.Single(journal.Began);
        var completed = Assert.Single(journal.Completed);
        Assert.Equal((began.RunId, SubagentRunStatus.Failed, "subagent_model_failed"), completed);
    }

    [Fact]
    public async Task ExecuteAsync_JournalsCancelledRun()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var setup = CreateSetup([new TextDelta("late")], journal, delay: TimeSpan.FromSeconds(3));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        // Cancelling from the first progress record lands after the journal begin and
        // before the model call, which keeps the interrupted transition deterministic.
        var context = CreateContext(timeout: TimeSpan.FromSeconds(10)) with
        {
            SubagentProgress = _ => cancellation.Cancel()
        };

        var result = await ExecuteAsync(setup.Tool, context, "parent-call-3", cancellation.Token);

        Assert.Equal("tool_cancelled", result.ErrorCode);
        var began = Assert.Single(journal.Began);
        var completed = Assert.Single(journal.Completed);
        Assert.Equal((began.RunId, SubagentRunStatus.Cancelled, (string?)null), completed);
        Assert.Equal(0, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RunsWithoutJournalingWhenTaskRowIsMissing()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = false };
        var setup = CreateSetup([new TextDelta("完成")], journal);

        var result = await ExecuteAsync(setup.Tool, CreateContext(), "parent-call-4",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(journal.Began);
        Assert.Empty(journal.Completed);
        Assert.Equal(1, setup.Provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_FailsClosedBeforeModelCallWhenJournalThrows()
    {
        var journal = new RecordingSubagentRunJournal
        {
            BeginResult = true, BeginFailure = new InvalidOperationException("synthetic-journal-secret")
        };
        var setup = CreateSetup([new TextDelta("unused")], journal);

        var result = await ExecuteAsync(setup.Tool, CreateContext(), "parent-call-5",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("subagent_journal_unavailable", result.ErrorCode);
        Assert.DoesNotContain("synthetic-journal-secret", result.Summary);
        Assert.Equal(0, setup.Provider.Calls);
        Assert.Null(setup.Provider.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_CompletionFailureNeverMasksTheRunResult()
    {
        var journal = new RecordingSubagentRunJournal
        {
            BeginResult = true, CompleteFailure = new InvalidOperationException("synthetic")
        };
        var setup = CreateSetup([new TextDelta("完成")], journal);

        var result = await ExecuteAsync(setup.Tool, CreateContext(), "parent-call-6",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(journal.Began);
        Assert.Empty(journal.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesOversizedErrorCodesBeforeJournaling()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var oversized = new string('e', 200);
        var setup = CreateSetup(
            [new ToolCallCompleted("child-1", "test.read", JsonSerializer.SerializeToElement(new { }))],
            journal);
        var context = CreateContext() with
        {
            SubagentTools = new StaticSubagentToolGateway(ToolResult.Failure("子工具失败。", oversized))
        };

        var result = await ExecuteAsync(setup.Tool, context, "parent-call-7",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        var completed = Assert.Single(journal.Completed);
        Assert.Equal(SubagentRunStatus.Failed, completed.Status);
        Assert.Equal(new string('e', 128), completed.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CompressesSnapshotsToFitTheColumnBound()
    {
        var journal = new RecordingSubagentRunJournal { BeginResult = true };
        var setup = CreateSetup([new TextDelta("完成")], journal,
            systemPrompt: new string('审', 40_000));
        var task = new string('改', 12_000);

        var result = await ExecuteAsync(setup.Tool, CreateContext(), "parent-call-8",
            TestContext.Current.CancellationToken, task);

        Assert.True(result.IsSuccess);
        var began = Assert.Single(journal.Began);
        Assert.True(began.SnapshotJson.Length <= 16_384);
        using var snapshot = JsonDocument.Parse(began.SnapshotJson);
        Assert.Equal(40_000, snapshot.RootElement.GetProperty("systemPromptLength").GetInt32());
        Assert.Equal(12_000, snapshot.RootElement.GetProperty("taskLength").GetInt32());
    }

    private static (
        SubagentTool Tool, StubModelProvider Provider,
        AiProvider ProviderRow, ModelProfile Model, SubagentProfile Profile) CreateSetup(
        IReadOnlyList<ModelEvent> events,
        RecordingSubagentRunJournal journal,
        TimeSpan delay = default,
        string systemPrompt = "检查代码并报告问题。")
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), provider.Id, "sub-model", "Sub model", 16_000, 2_000,
            "low", null, null, true, false, false, true, "{}");
        var profile = new SubagentProfile(
            Guid.NewGuid(), "review", "独立审查改动", systemPrompt,
            provider.Id, model.Id, "high", true, true);
        var modelProvider = new StubModelProvider(provider.Protocol, events, delay);
        var registry = new StubModelProviderRegistry(modelProvider);
        var tool = new SubagentTool(
            new InMemorySubagentConfigurationRepository([profile]),
            new InMemoryConfigurationRepository(provider, model),
            registry,
            journal);
        return (tool, modelProvider, provider, model, profile);
    }

    private static async Task<ToolResult> ExecuteAsync(
        SubagentTool tool,
        ToolExecutionContext context,
        string callId,
        CancellationToken cancellationToken,
        string task = "检查改动")
    {
        var executor = new ToolExecutor(new ToolRegistry([tool]), new FixedToolApprovalService(true));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { agent = "review", task }));
        var request = new ToolRequest(callId, SubagentTool.ToolName, document.RootElement.Clone());
        return await executor.ExecuteAsync(request, context, cancellationToken);
    }

    private static ToolExecutionContext CreateContext(TimeSpan? timeout = null) =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess,
            64 * 1024, timeout);
}
