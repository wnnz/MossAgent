using System.Text.Json;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AgentWorkspaceFlowTests
{
    [Theory]
    [InlineData("high")]
    [InlineData("ultra")]
    public async Task SettingsSavedAgent_DrivesTheActualModelPromptAndReasoning(string reasoning)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var settings = fixture.Main.Agents;
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "review";
        settings.SystemPrompt = "agent-configured-prompt-marker";
        settings.ReasoningEffort = reasoning;
        settings.SelectedModel = Assert.Single(settings.Models, option => option.Model.Id == fixture.AlternativeModel.Id);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError, settings.Status);
        var profile = Assert.Single(fixture.Workspace.Agents);
        fixture.Workspace.SelectAgentCommand.Execute(profile);
        Assert.Equal(fixture.AlternativeModel.Id, fixture.Workspace.SelectedModel?.Id);
        Assert.Equal(reasoning, fixture.Workspace.ModelPicker.EffectiveReasoningEffort);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed
            && !fixture.Workspace.IsRunning);

        var request = Assert.Single(fixture.Server.Requests);
        Assert.Equal(fixture.AlternativeModel.ModelId, request.GetProperty("model").GetString());
        Assert.Equal(reasoning, request.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("agent-configured-prompt-marker", SystemPrompt(request));
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
    }

    [Fact]
    public async Task SavingAnotherDraft_DoesNotChangeTheRunningRequestButUpdatesTheNextTurn()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var settings = fixture.Main.Agents;
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "review";
        settings.SystemPrompt = "captured-agent-prompt";
        settings.ReasoningEffort = "high";
        settings.SelectedModel = Assert.Single(settings.Models, option => option.Model.Id == fixture.AlternativeModel.Id);
        await settings.SaveCommand.ExecuteAsync(null);
        fixture.Workspace.SelectAgentCommand.Execute(Assert.Single(fixture.Workspace.Agents));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        settings.SystemPrompt = "next-agent-prompt";
        settings.ReasoningEffort = "low";
        settings.SelectedModel = Assert.Single(settings.Models, option => option.Model.Id == fixture.SelectedModel.Id);

        await settings.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.Workspace.IsRunning);
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        var first = Assert.Single(fixture.Server.Requests);
        Assert.Equal(fixture.AlternativeModel.ModelId, first.GetProperty("model").GetString());
        Assert.Equal("high", first.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("captured-agent-prompt", SystemPrompt(first));
        Assert.DoesNotContain("next-agent-prompt", SystemPrompt(first));
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask?.Status);
        fixture.Workspace.ComposerText = "使用更新后的代理继续。";
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !fixture.Workspace.IsRunning);

        var second = fixture.Server.Requests[1];
        Assert.Equal(fixture.SelectedModel.ModelId, second.GetProperty("model").GetString());
        Assert.Equal("low", second.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("next-agent-prompt", SystemPrompt(second));
        Assert.DoesNotContain("captured-agent-prompt", SystemPrompt(second));
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
    }

    private static string SystemPrompt(JsonElement request) => request.GetProperty("input").EnumerateArray()
        .First(item => item.GetProperty("role").GetString() == "system").GetProperty("content").GetString()!;
}
