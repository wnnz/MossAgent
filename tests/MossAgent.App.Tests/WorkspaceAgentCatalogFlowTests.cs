using System.Text.Json;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentCatalogFlowTests
{
    [Fact]
    public async Task DisabledSelection_DoesNotSilentlySendUsingTheDefaultAgent()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var profile = await SaveAgentAsync(fixture);
        fixture.Workspace.SelectAgentCommand.Execute(profile);
        var draft = fixture.Workspace.ComposerText;

        await fixture.AgentProfiles.SaveAsync(profile with { IsEnabled = false }, fixture.Token);
        await fixture.Workspace.ReloadAgentsAsync();

        Assert.Equal(profile.Id, fixture.Workspace.SelectedAgent?.Id);
        Assert.True(fixture.Workspace.HasAgents);
        Assert.False(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task ChoosingAnAgentWithAnUnavailableModel_DoesNotKeepSendingThePreviousModel()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var profile = await SaveAgentAsync(fixture);
        await fixture.Configurations.SaveModelAsync(fixture.AlternativeModel with { IsEnabled = false }, fixture.Token);
        await fixture.Workspace.ModelPicker.ReloadAsync();

        fixture.Workspace.SelectAgentCommand.Execute(profile);

        Assert.Equal(profile.Id, fixture.Workspace.SelectedAgent?.Id);
        Assert.False(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task ModelCatalogRefresh_PreservesAgentIdentityUntilItsModelReturns()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var profile = await SaveAgentAsync(fixture);
        fixture.Workspace.SelectAgentCommand.Execute(profile);
        await fixture.Configurations.SaveModelAsync(fixture.AlternativeModel with { IsEnabled = false }, fixture.Token);

        await fixture.Workspace.ModelPicker.ReloadAsync();
        await fixture.Workspace.ReloadAgentsAsync();

        Assert.Equal(profile.Id, fixture.Workspace.SelectedAgent?.Id);
        Assert.False(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        await fixture.Configurations.SaveModelAsync(fixture.AlternativeModel, fixture.Token);
        await fixture.Workspace.ModelPicker.ReloadAsync();
        await fixture.Workspace.ReloadAgentsAsync();
        Assert.Equal(profile.Id, fixture.Workspace.SelectedAgent?.Id);
        Assert.Equal(fixture.AlternativeModel.Id, fixture.Workspace.SelectedModel?.Id);
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
    }

    [Fact]
    public async Task AStaleMenuItem_UsesTheCurrentAgentConfigurationInTheActualRequest()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var stale = await SaveAgentAsync(fixture);
        var current = stale with
        {
            ModelProfileId = fixture.SelectedModel.Id,
            ProviderId = fixture.SelectedProvider.Id,
            SystemPrompt = "current-agent-prompt",
            ReasoningEffort = "ultra"
        };
        await fixture.AgentProfiles.SaveAsync(current, fixture.Token);
        await fixture.Workspace.ReloadAgentsAsync();

        fixture.Workspace.SelectAgentCommand.Execute(stale);

        Assert.Equal(current, fixture.Workspace.SelectedAgent);
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning
            && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed);
        var request = Assert.Single(fixture.Server.Requests);
        Assert.Equal(fixture.SelectedModel.ModelId, request.GetProperty("model").GetString());
        Assert.Equal("ultra", request.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("current-agent-prompt", SystemPrompt(request));
        Assert.DoesNotContain("old-agent-prompt", SystemPrompt(request));
    }

    private static async Task<AgentProfile> SaveAgentAsync(WorkspaceModelSelectionFixture fixture)
    {
        var profile = new AgentProfile(Guid.NewGuid(), "review", "old-agent-prompt",
            fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id, "high", true);
        await fixture.AgentProfiles.SaveAsync(profile, fixture.Token);
        await fixture.Workspace.ReloadAgentsAsync();
        return Assert.Single(fixture.Workspace.Agents);
    }

    private static string SystemPrompt(JsonElement request) => request.GetProperty("input").EnumerateArray()
        .First(item => item.GetProperty("role").GetString() == "system").GetProperty("content").GetString()!;
}
