using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceConversationPreferenceLifetimeTests
{
    [Fact]
    public async Task TimedOutRead_BlocksSendingAndRejectsANoncooperativeLateResult()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var read = new TaskCompletionSource<ConversationModelPreference?>();
        var repository = new ConversationPreferenceProbeRepository { Read = (_, _) => read.Task };
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker());
        await workspace.LoadAsync();
        workspace.ConversationPreferences!.Dispose();
        using var preferences = new WorkspaceConversationPreferences(repository, workspace.Catalog,
            workspace.ModelPicker, workspace.AgentPicker, TimeSpan.FromMilliseconds(40));
        workspace.Catalog.SelectedTask = Assert.Single(workspace.Catalog.Tasks, item => item.Id == task.Id);

        await fixture.WaitUntilAsync(() => workspace.ModelPicker.CatalogNotice.Contains("无法恢复", StringComparison.Ordinal));
        Assert.True(repository.ReadToken.IsCancellationRequested);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);
        read.SetResult(new(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id, "high"));
        await Task.Delay(30, fixture.Token);
        Assert.NotEqual(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task CallerCancellation_CancelsRefreshWithoutPublishingTheLateResult()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await AddTaskAsync(fixture);
        var read = new TaskCompletionSource<ConversationModelPreference?>();
        var repository = new ConversationPreferenceProbeRepository { Read = (_, _) => read.Task };
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker());
        await workspace.LoadAsync();
        workspace.ConversationPreferences!.Dispose();
        using var preferences = new WorkspaceConversationPreferences(repository, workspace.Catalog, workspace.ModelPicker, workspace.AgentPicker);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var refreshing = preferences.RefreshAsync(cancellation.Token);

        await cancellation.CancelAsync();
        await refreshing.WaitAsync(fixture.Token);
        Assert.True(repository.ReadToken.IsCancellationRequested);
        read.SetResult(new(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        await Task.Delay(30, fixture.Token);

        Assert.NotEqual(fixture.AlternativeModel.Id, workspace.SelectedModel?.Id);
        Assert.False(workspace.ModelPicker.CanSendWithSelectedModel);
    }

    [Fact]
    public async Task Disposal_CancelsTheReadAndRetiresInputSubscriptions()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await AddTaskAsync(fixture);
        var read = new TaskCompletionSource<ConversationModelPreference?>();
        var repository = new ConversationPreferenceProbeRepository { Read = (_, _) => read.Task };
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker());
        await workspace.LoadAsync();
        workspace.ConversationPreferences!.Dispose();
        var preferences = new WorkspaceConversationPreferences(repository, workspace.Catalog, workspace.ModelPicker, workspace.AgentPicker);
        var notice = workspace.ModelPicker.CatalogNotice;

        preferences.Dispose();
        Assert.True(repository.ReadToken.IsCancellationRequested);
        read.SetResult(new(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        await Task.Delay(30, fixture.Token);
        Assert.Equal(notice, workspace.ModelPicker.CatalogNotice);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ModelPicker.SelectedReasoning = "高";
        Assert.Empty(repository.Writes);
    }

    [Fact]
    public async Task NoncooperativeWrite_RetainsOwnershipAfterItsDeadlineUntilTheLatestChoiceIsSaved()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        var write = new TaskCompletionSource<bool>();
        var repository = new ConversationPreferenceProbeRepository { Save = _ => write.Task };
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, new WorkspaceRunTracker());
        await workspace.LoadAsync();
        workspace.ConversationPreferences!.Dispose();
        using var preferences = new WorkspaceConversationPreferences(repository, workspace.Catalog,
            workspace.ModelPicker, workspace.AgentPicker, TimeSpan.FromMilliseconds(40));
        var first = new ConversationModelPreference(fixture.SelectedModel.ProviderId, fixture.SelectedModel.Id, "high");
        var last = new ConversationModelPreference(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id, "low");
        preferences.Remember(task.Id, first);

        await fixture.WaitUntilAsync(() => repository.WriteToken.IsCancellationRequested);
        preferences.Remember(task.Id, last);
        Assert.Single(repository.Writes);
        var stopping = preferences.StopAsync();
        Assert.False(stopping.IsCompleted);
        write.SetResult(true);
        await stopping.WaitAsync(fixture.Token);

        Assert.Equal(2, repository.Writes.Count);
        Assert.Equal(last, repository.Stored[task.Id]);
    }

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "lifetime fixture",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        return task;
    }
}
