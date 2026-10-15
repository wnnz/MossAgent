using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceConversationPreferenceTests
{
    [Fact]
    public async Task SwitchingConversations_RestoresIndependentChoicesWithoutWritingDuringRestore()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture);
        var second = await AddTaskAsync(fixture);
        var repository = new ConversationPreferenceProbeRepository();
        repository.Stored[first.Id] = Preference(fixture.AlternativeModel, "high");
        repository.Stored[second.Id] = Preference(fixture.SelectedModel, null);
        using var workspace = await OpenAsync(fixture, repository);

        SelectTask(workspace, first.Id);
        Assert.Equal(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.Equal("high", workspace.ModelPicker.EffectiveReasoningEffort);
        SelectTask(workspace, second.Id);
        Assert.Equal(fixture.SelectedModel.Id, workspace.SelectedModel?.Id);
        Assert.Equal("默认", workspace.ModelPicker.SelectedReasoning);
        SelectTask(workspace, first.Id);
        Assert.Equal(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task AnImplicitChoice_RemainsUnavailableAfterSwitchingInsteadOfFallingBack()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture);
        var second = await AddTaskAsync(fixture);
        var repository = new ConversationPreferenceProbeRepository();
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, first.Id);
        Assert.Equal(fixture.SelectedModel.Id, workspace.SelectedModel?.Id);
        await fixture.Configurations.SaveModelAsync(fixture.SelectedModel with { IsEnabled = false }, fixture.Token);
        await workspace.ModelPicker.ReloadAsync();
        await workspace.ReloadAgentsAsync();

        SelectTask(workspace, second.Id);
        SelectTask(workspace, first.Id);

        Assert.True(workspace.ModelPicker.IsSelectedUnavailable);
        Assert.Null(workspace.SelectedModel);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task ADelayedRead_CannotOverwriteTheNextConversation()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture);
        var second = await AddTaskAsync(fixture);
        var delayed = new TaskCompletionSource<ConversationModelPreference?>();
        var repository = new ConversationPreferenceProbeRepository
        {
            Read = (id, _) => id == first.Id ? delayed.Task
                : Task.FromResult<ConversationModelPreference?>(Preference(fixture.SelectedModel, "low"))
        };
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, first.Id);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);

        SelectTask(workspace, second.Id);
        delayed.SetResult(Preference(fixture.AlternativeModel, "high"));
        await Task.Delay(30, fixture.Token);

        Assert.Equal(second.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(fixture.SelectedModel.Id, workspace.SelectedModel?.Id);
        Assert.Equal("low", workspace.ModelPicker.EffectiveReasoningEffort);
        Assert.True(workspace.ModelPicker.CanSendWithSelectedModel);
    }

    [Fact]
    public async Task ExplicitSelection_OverridesALateReadAndIsSavedToTheCorrectTask()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var delayed = new TaskCompletionSource<ConversationModelPreference?>();
        var repository = new ConversationPreferenceProbeRepository { Read = (_, _) => delayed.Task };
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, task.Id);

        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ModelPicker.SelectedReasoning = "高";
        delayed.SetResult(Preference(fixture.SelectedModel, null));
        await workspace.ConversationPreferences!.FlushAsync();
        await Task.Delay(30, fixture.Token);

        Assert.Equal(Preference(fixture.AlternativeModel, "high"), repository.Stored[task.Id]);
        Assert.Equal(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.True(workspace.ModelPicker.CanSendWithSelectedModel);
    }

    [Fact]
    public async Task FailedRead_BlocksSendingWithoutLeakingExceptionTextAndAllowsAnExplicitChoice()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var repository = new ConversationPreferenceProbeRepository
        {
            Read = (_, _) => Task.FromException<ConversationModelPreference?>(new InvalidOperationException("synthetic-secret"))
        };
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, task.Id);
        workspace.ComposerText = "do not silently send";

        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Contains("无法恢复", workspace.ModelPicker.CatalogNotice);
        Assert.DoesNotContain("synthetic-secret", workspace.ModelPicker.CatalogNotice);
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingSavedModel_IsNotReplacedByTheDefaultModel()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var repository = new ConversationPreferenceProbeRepository();
        repository.Stored[task.Id] = new(fixture.SelectedProvider.Id, Guid.NewGuid(), "high");
        using var workspace = await OpenAsync(fixture, repository);

        SelectTask(workspace, task.Id);

        Assert.Null(workspace.SelectedModel);
        Assert.True(workspace.ModelPicker.IsSelectedUnavailable);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task AFailedWrite_PreservesTheChoiceAndRefreshRetriesWithoutExposingExceptionText()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var repository = new ConversationPreferenceProbeRepository
        {
            Save = _ => Task.FromException<bool>(new InvalidOperationException("synthetic-secret"))
        };
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, task.Id);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        await workspace.ConversationPreferences!.FlushAsync();

        Assert.Equal(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.Contains("尚未保存", workspace.ModelPicker.CatalogNotice);
        Assert.DoesNotContain("synthetic-secret", workspace.ModelPicker.CatalogNotice);
        repository.Save = null;
        await workspace.ModelPicker.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(Preference(fixture.AlternativeModel, null), repository.Stored[task.Id]);
        Assert.False(workspace.ModelPicker.HasCatalogNotice);
    }

    [Fact]
    public async Task RapidChanges_AreSerializedAndCoalescedToTheLatestChoice()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var write = new TaskCompletionSource<bool>();
        var repository = new ConversationPreferenceProbeRepository { Save = _ => write.Task };
        using var workspace = await OpenAsync(fixture, repository);
        SelectTask(workspace, task.Id);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ModelPicker.SelectedReasoning = "高";
        workspace.ModelPicker.SelectedReasoning = "低";
        workspace.ModelPicker.SelectedReasoning = "Ultra";
        Assert.Single(repository.Writes);
        var stopping = workspace.ConversationPreferences!.StopAsync();
        Assert.False(stopping.IsCompleted);

        write.SetResult(true);
        await stopping;

        Assert.Equal(2, repository.Writes.Count);
        Assert.Equal(Preference(fixture.AlternativeModel, "ultra"), repository.Stored[task.Id]);
    }

    private static ConversationModelPreference Preference(ModelProfile model, string? reasoning) =>
        new(model.ProviderId, model.Id, reasoning);

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "preference fixture",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        return task;
    }

    private static async Task<WorkspaceViewModel> OpenAsync(WorkspaceModelSelectionFixture fixture,
        ConversationPreferenceProbeRepository repository)
    {
        var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker(), conversationPreferences: repository);
        await workspace.LoadAsync();
        return workspace;
    }

    private static void SelectTask(WorkspaceViewModel workspace, Guid taskId) =>
        workspace.Catalog.SelectedTask = Assert.Single(workspace.Catalog.Tasks, task => task.Id == taskId);
}
