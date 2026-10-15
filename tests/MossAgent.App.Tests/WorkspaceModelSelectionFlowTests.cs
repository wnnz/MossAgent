using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceModelSelectionFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisablingASelection_BlocksSendingUntilTheUserRestoresOrExplicitlyReplacesIt(bool provider)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var draft = workspace.ComposerText;
        workspace.ModelPicker.SelectedReasoning = "高";
        if (provider)
        {
            fixture.Main.Providers.EditProviderCommand.Execute(fixture.SelectedProvider);
            fixture.Main.Providers.IsEnabled = false;
            await fixture.Main.Providers.SaveCommand.ExecuteAsync(null);
            Assert.False(fixture.Main.Providers.HasValidationError, fixture.Main.Providers.Status);
        }
        else await fixture.SetSelectedModelEnabledAsync(false);

        Assert.Null(workspace.SelectedModel);
        Assert.Null(workspace.SelectedProvider);
        Assert.True(workspace.ModelPicker.HasModels);
        Assert.True(workspace.ModelPicker.IsSelectedUnavailable);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Equal(draft, workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(workspace.Catalog.Tasks);
        if (!provider)
        {
            await fixture.SetSelectedModelEnabledAsync(true);
            Assert.Equal(fixture.SelectedModel.Id, workspace.SelectedModel?.Id);
            Assert.Equal("high", workspace.ModelPicker.EffectiveReasoningEffort);
            Assert.Equal(draft, workspace.ComposerText);
            await fixture.SetSelectedModelEnabledAsync(false);
        }

        var alternative = fixture.AlternativeModel;
        Assert.True(workspace.ModelPicker.TrySelect(alternative.ProviderId, alternative.Id));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed && !workspace.IsRunning);

        var request = Assert.Single(fixture.Server.Requests);
        Assert.Equal(alternative.ModelId, request.GetProperty("model").GetString());
        Assert.Equal("high", request.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains(workspace.Messages, message => message.Content.Contains("模型选择链路已完成。", StringComparison.Ordinal));
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
    }

    [Fact]
    public async Task SettingsUpdates_AreUsedByTheActualRequestEvenAfterAnOldMenuItemIsActivated()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = fixture.Workspace.ModelPicker;
        var stale = picker.SelectedItem;
        fixture.Main.Models.EditProviderModelCommand.Execute(fixture.SelectedModel);
        fixture.Main.Models.ContextLength = 64000;
        fixture.Main.Models.MaximumOutputTokens = 500;
        fixture.Main.Models.ReasoningEffort = "xhigh";
        fixture.Main.Models.DisplayName = "updated";
        await fixture.Main.Models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(fixture.Main.Models.HasStatusError, fixture.Main.Models.Status);
        picker.SelectItemCommand.Execute(stale);
        Assert.Equal(64000, fixture.Workspace.SelectedModel?.ContextLength);
        Assert.Equal("updated", picker.SelectedModelName);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed
            && !fixture.Workspace.IsRunning);

        var request = Assert.Single(fixture.Server.Requests);
        Assert.Equal(fixture.SelectedModel.ModelId, request.GetProperty("model").GetString());
        Assert.Equal(500, request.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal("xhigh", request.GetProperty("reasoning").GetProperty("effort").GetString());
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
    }

    [Fact]
    public async Task DisablingTheModel_DoesNotHideTheRunningTasksStopAction()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);

        await fixture.SetSelectedModelEnabledAsync(false);

        Assert.False(workspace.ModelPicker.CanUseSelectedModel);
        Assert.True(workspace.IsRunning);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Cancelled && !workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Cancelled);
    }

    [Fact]
    public async Task DisposingTheFixture_CancelsAndDrainsRunningTasks()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);

        await fixture.DisposeAsync();

        Assert.False(workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
    }
}
