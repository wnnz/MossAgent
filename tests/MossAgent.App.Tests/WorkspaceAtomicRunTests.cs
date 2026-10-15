using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAtomicRunTests
{
    [Fact]
    public async Task CompletionFailurePersistsTheStreamedReplyExactlyOnceAsFailed()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.RejectCompletionAsync();
        using var state = fixture.CreateState();

        await RunAsync(fixture, state, emitTool: false, rejectPresentation: false);

        var history = await fixture.Repository.GetMessagesAsync(state.Task.Id, fixture.Token);
        Assert.Equal("streamed response", Assert.Single(history, message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(AgentTaskStatus.Failed, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Status);
        Assert.Empty(state.PendingAssistantContent);
    }

    [Fact]
    public async Task PresentationFailureDoesNotDuplicateAlreadyCommittedAssistantSegment()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();

        await RunAsync(fixture, state, emitTool: true, rejectPresentation: true);

        var history = await fixture.Repository.GetMessagesAsync(state.Task.Id, fixture.Token);
        Assert.Single(history, message => message.Role == MessageRole.Tool);
        Assert.Equal("streamed response", Assert.Single(history, message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(AgentTaskStatus.Failed, state.Task.Status);
    }

    [Fact]
    public async Task ToolCommitFailureRollsBackItsAssistantSegmentBeforeFailureFinalization()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.RejectToolMessageAsync();
        using var state = fixture.CreateState();

        await RunAsync(fixture, state, emitTool: true, rejectPresentation: false);

        var history = await fixture.Repository.GetMessagesAsync(state.Task.Id, fixture.Token);
        Assert.DoesNotContain(history, message => message.Role == MessageRole.Tool);
        Assert.Equal("streamed response", Assert.Single(history, message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(AgentTaskStatus.Failed, state.Task.Status);
    }

    private static async Task RunAsync(WorkspacePersistenceFixture fixture, WorkspaceRunState state, bool emitTool, bool rejectPresentation)
    {
        using var runs = new WorkspaceRunTracker();
        runs.Add(state);
        var controller = fixture.CreateController(new PersistenceProbeAgentRunner(emitTool), runs);
        await controller.RunAsync(state, fixture.Panels, (current, agentEvent) =>
        {
            if (rejectPresentation && agentEvent is AgentToolEvent) throw new InvalidOperationException("synthetic presentation failure");
            current.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, current.Assistant, current.Messages,
                status => current.Activity = status, _ => { });
            return Task.CompletedTask;
        }, (current, status) => current.Activity = status, _ => { }, () => { });
        Assert.False(runs.IsRunning(state.Task.Id));
    }
}
