using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSubagentLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StopCommand_CancelsChildConnectionAndRestoresItsPartialDialogue(bool reopen, bool chunked)
    {
        await using var fixture = await WorkspaceSubagentLifetimeFixture.CreateAsync(chunked);
        await fixture.StartAsync();
        Assert.True(fixture.Server.ChildFrameWritten.Task.IsCompletedSuccessfully);
        var taskId = fixture.Workspace.Catalog.SelectedTask!.Id;
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning);
        await fixture.Server.ChildDisconnected.Task.WaitAsync(fixture.Token);
        await fixture.Server.Completion.WaitAsync(fixture.Token);

        Assert.Equal(AgentTaskStatus.Cancelled, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.False(fixture.Services.GetRequiredService<ApprovalViewModel>().IsPending);
        if (reopen) await fixture.ReopenAsync(taskId);
        AssertCancelledCard(Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool));
        var history = await fixture.Repository.GetMessagesAsync(taskId, fixture.Token);
        AssertCancelledCard(WorkspaceConversationMapper.ToChatMessage(Assert.Single(history, message => message.Role == MessageRole.Tool)));
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(taskId, 10, fixture.Token));
        Assert.True(audit.ApprovalGranted);
        Assert.False(audit.IsSuccess);
        Assert.Equal("tool_cancelled", audit.ErrorCode);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Equal(["parent-lifetime", "child-lifetime"], fixture.Server.Requests.Select(request => request.GetProperty("model").GetString()));
        Assert.Equal("high", fixture.Server.Requests[1].GetProperty("reasoning").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task PresentationFailure_DrainsChildAndAuditBeforeTheTaskFinishes()
    {
        await using var fixture = await WorkspaceSubagentLifetimeFixture.CreateAsync();
        var state = await CreateStateAsync(fixture);
        using var runs = new WorkspaceRunTracker();
        runs.Add(state);
        var factory = fixture.Services.GetRequiredService<WorkspaceTaskFactory>();
        var controller = new WorkspaceRunController(fixture.Services.GetRequiredService<WorkspaceRunExecutor>(), runs, factory);
        const string secret = "Authorization: Bearer synthetic-presentation-secret";
        await controller.RunAsync(state, fixture.Services.GetRequiredService<WorkspaceTaskPanelCoordinator>(),
            (current, agentEvent) =>
            {
                current.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, current.Assistant,
                    current.Messages, status => current.Activity = status, _ => { });
                if (agentEvent is AgentSubagentProgressEvent { Message.IsPartial: true })
                    throw new InvalidOperationException(secret);
                return Task.CompletedTask;
            }, (current, status) => current.Activity = status, _ => { }, () => { });

        await fixture.Server.ChildDisconnected.Task.WaitAsync(fixture.Token);
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(state.Task.Id, 10, fixture.Token));
        Assert.False(audit.IsSuccess);
        Assert.Equal("tool_cancelled", audit.ErrorCode);
        Assert.False(runs.IsRunning(state.Task.Id));
        var card = Assert.Single(state.Messages, message => message.IsSubagentTool);
        Assert.False(card.IsToolRunning);
        Assert.Equal("失败", card.ToolStatus);
        Assert.DoesNotContain(secret, card.Tool!.Summary);
        Assert.DoesNotContain(secret, state.Activity);
        var task = Assert.Single(await fixture.Repository.GetTasksAsync(state.Task.ProjectId, fixture.Token));
        Assert.Equal(AgentTaskStatus.Failed, task.Status);
        Assert.Equal(2, fixture.Server.Requests.Count);
    }

    private static void AssertCancelledCard(ChatMessageViewModel card)
    {
        Assert.False(card.IsToolRunning);
        Assert.Equal("已取消", card.ToolStatus);
        Assert.Equal("tool_cancelled", card.Tool!.ErrorCode);
        Assert.Contains(card.Tool.Dialogue!, entry => entry.Role == "子代理" && entry.Content == SubagentLifetimeModelServer.PartialReply);
        Assert.All(card.Tool.Dialogue!, entry => Assert.False(entry.IsPartial));
    }

    private static async Task<WorkspaceRunState> CreateStateAsync(WorkspaceSubagentLifetimeFixture fixture)
    {
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = await fixture.Services.GetRequiredService<WorkspaceTaskFactory>()
            .CreateAsync(project, "检查代码", ApprovalPolicy.FullAccess, false, fixture.Token);
        var configurations = fixture.Services.GetRequiredService<IConfigurationRepository>();
        var provider = Assert.Single(await configurations.GetProvidersAsync(fixture.Token));
        var model = Assert.Single(await configurations.GetModelsAsync(provider.Id, fixture.Token), model => model.IsDefault);
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        return new WorkspaceRunState(task, project, provider, model,
            new ObservableCollection<ChatMessageViewModel> { new("你", "检查代码"), assistant }, assistant);
    }
}
