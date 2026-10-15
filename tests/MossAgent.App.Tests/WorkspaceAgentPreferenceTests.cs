using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentPreferenceTests
{
    [Fact]
    public async Task MissingSavedAgent_RetainsItsIdentityAndRecoversWithoutWritingADefault()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var agent = Agent(fixture);
        var repository = new ConversationPreferenceProbeRepository();
        repository.Stored[task.Id] = new(agent.ProviderId, agent.ModelProfileId, null, agent.Id, true);
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker(), conversationPreferences: repository);
        await workspace.LoadAsync();
        SelectTask(workspace, task.Id);

        Assert.Null(workspace.SelectedAgent);
        Assert.Equal(agent.Id, workspace.AgentPicker.PreferredProfileId);
        Assert.True(workspace.HasAgents);
        Assert.True(workspace.AgentPicker.IsSelectedUnavailable);
        Assert.Contains("不可用", workspace.SelectedAgentLabel);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);

        await fixture.AgentProfiles.SaveAsync(agent, fixture.Token);
        await workspace.ReloadAgentsAsync();

        Assert.Equal(agent.Id, workspace.SelectedAgent?.Id);
        Assert.Equal(agent.ModelProfileId, workspace.SelectedModel?.Id);
        Assert.Equal("low", workspace.ModelPicker.EffectiveReasoningEffort);
        Assert.True(workspace.ModelPicker.CanSendWithSelectedModel);
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task ExplicitAgentChoice_PersistsItsDefaultSourceAndTracksConfigurationUpdates()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var agent = Agent(fixture);
        await fixture.AgentProfiles.SaveAsync(agent, fixture.Token);
        var repository = new ConversationPreferenceProbeRepository();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker(), conversationPreferences: repository);
        await workspace.LoadAsync();
        SelectTask(workspace, task.Id);
        workspace.SelectAgentCommand.Execute(agent);
        await workspace.ConversationPreferences!.FlushAsync();

        Assert.Equal(new(agent.ProviderId, agent.ModelProfileId, null, agent.Id, true), repository.Stored[task.Id]);
        var current = agent with { ProviderId = fixture.SelectedProvider.Id, ModelProfileId = fixture.SelectedModel.Id, ReasoningEffort = "ultra" };
        await fixture.AgentProfiles.SaveAsync(current, fixture.Token);
        await workspace.ReloadAgentsAsync();

        Assert.Equal(current, workspace.SelectedAgent);
        Assert.Equal(current.ModelProfileId, workspace.SelectedModel?.Id);
        Assert.Equal("ultra", workspace.ModelPicker.EffectiveReasoningEffort);
        Assert.Single(repository.Writes);
    }

    [Fact]
    public async Task UserReasoningOverride_SurvivesAgentRefreshAndConversationSwitching()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var other = await AddTaskAsync(fixture);
        var agent = Agent(fixture);
        await fixture.AgentProfiles.SaveAsync(agent, fixture.Token);
        var repository = new ConversationPreferenceProbeRepository();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker(), conversationPreferences: repository);
        await workspace.LoadAsync();
        SelectTask(workspace, task.Id);
        workspace.SelectAgentCommand.Execute(agent);
        workspace.ModelPicker.SelectedReasoning = "高";
        await workspace.ConversationPreferences!.FlushAsync();
        var current = agent with { ProviderId = fixture.SelectedProvider.Id, ModelProfileId = fixture.SelectedModel.Id, ReasoningEffort = "medium" };
        await fixture.AgentProfiles.SaveAsync(current, fixture.Token);
        await workspace.ReloadAgentsAsync();

        Assert.Equal("high", workspace.ModelPicker.EffectiveReasoningEffort);
        SelectTask(workspace, other.Id);
        Assert.Null(workspace.SelectedAgent);
        SelectTask(workspace, task.Id);

        Assert.Equal(current, workspace.SelectedAgent);
        Assert.Equal(current.ModelProfileId, workspace.SelectedModel?.Id);
        Assert.Equal("high", workspace.ModelPicker.EffectiveReasoningEffort);
        Assert.False(repository.Stored[task.Id].FollowAgentReasoning);
        Assert.Equal("high", repository.Stored[task.Id].ReasoningEffort);
    }

    private static AgentProfile Agent(WorkspaceModelSelectionFixture fixture) => new(Guid.NewGuid(), "fixture-agent",
        "synthetic instructions", fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id, "low", true);

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "preference fixture",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        return task;
    }

    private static void SelectTask(WorkspaceViewModel workspace, Guid taskId) =>
        workspace.Catalog.SelectedTask = Assert.Single(workspace.Catalog.Tasks, task => task.Id == taskId);
}
