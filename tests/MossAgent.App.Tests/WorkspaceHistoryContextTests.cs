using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceHistoryContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubsequentSendIncludesPersistedToolEvidenceWithoutReplayingNativeCalls(bool subagent)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var result = subagent
            ? ToolResult.Success("delegation finished", dialogue: [new ToolDialogueMessage("assistant", "unique historical evidence")])
            : ToolResult.Success("read finished", "unique historical evidence");
        var stored = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent(subagent ? "subagent.run" : "filesystem.read_file", "previous-call", result)),
            DateTimeOffset.UtcNow, "previous-call");
        await fixture.Repository.AppendMessageAsync(stored, fixture.Token);
        var task = await fixture.Factory.CompleteAsync(fixture.Task, "Finished the previous operation.", AgentTaskStatus.Completed, fixture.Token);
        var resumed = await fixture.Factory.ResumeAsync(task, "Continue using the earlier result.", ApprovalPolicy.ReadOnly, fixture.Token);
        using var state = fixture.CreateState();
        state.Task = resumed;
        var runner = new HistoryCaptureAgentRunner();
        var executor = new WorkspaceRunExecutor(fixture.Repository, runner, fixture.Factory);

        await executor.ExecuteAsync(state, fixture.Panels.CreateContext(state.Task, fixture.Project), _ => Task.CompletedTask, fixture.Token);

        var messages = runner.Request!.Messages;
        var observation = Assert.Single(messages, message => message.Content.Contains("unique historical evidence", StringComparison.Ordinal));
        Assert.NotEqual(ModelRole.System, observation.Role);
        Assert.NotEqual(ModelRole.Tool, observation.Role);
        Assert.Equal(ModelRole.ToolObservation, observation.Role);
        Assert.Null(observation.ToolCalls);
        Assert.Null(observation.ToolCallId);
        Assert.Equal("Continue using the earlier result.", messages.Last().Content);
        var persisted = await fixture.Repository.GetMessagesAsync(state.Task.Id, fixture.Token);
        Assert.Equal(stored.Content, Assert.Single(persisted, message => message.Role == MessageRole.Tool).Content);
    }

    [Theory]
    [InlineData("legacy output")]
    [InlineData("not valid JSON {\"ToolName\":")]
    public void LegacyAndMalformedStoredResultsAreRetainedAsNonExecutableData(string content)
    {
        var message = new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), MessageRole.Tool, content,
            DateTimeOffset.UtcNow, "old-call");
        var mapped = WorkspaceConversationMapper.ToModelMessage(message);
        Assert.Equal(ModelRole.ToolObservation, mapped.Role);
        Assert.Contains(content, mapped.Content);
        Assert.Null(mapped.ToolCallId);
    }

    [Fact]
    public void UnknownPersistedRoleCannotBeEscalatedToSystemInstruction() =>
        Assert.Throws<InvalidOperationException>(() => WorkspaceConversationMapper.ToModelMessage(
            new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), (MessageRole)99, "untrusted", DateTimeOffset.UtcNow)));

    [Fact]
    public async Task ForeignTaskHistoryIsRejectedBeforeAnyModelRequestOrPersistence()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var repository = new TestWorkspaceRepository
        {
            HistoryOverride = [new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), MessageRole.Tool, "foreign evidence", DateTimeOffset.UtcNow)]
        };
        var runner = new HistoryCaptureAgentRunner();
        var executor = new WorkspaceRunExecutor(repository, runner, fixture.Factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(state,
            fixture.Panels.CreateContext(state.Task, fixture.Project), _ => Task.CompletedTask, fixture.Token));
        Assert.Null(runner.Request);
        Assert.Null(repository.SavedTask);
    }
}
