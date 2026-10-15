using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentEditorLifetimeTests
{
    [Fact]
    public async Task ReentryAndOtherMutations_CannotCancelOrOverlapTheAcceptedSave()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => gate.Task;
        settings.SystemPrompt = "updated prompt";
        var saving = settings.SaveCommand.ExecuteAsync(null);
        try
        {
            Assert.True(settings.IsBusy);
            Assert.Equal("正在保存…", settings.SaveLabel);
            Assert.False(settings.SaveCommand.CanExecute(null));
            Assert.False(settings.DeleteCommand.CanExecute(null));
            Assert.False(settings.RefreshCommand.CanExecute(null));
            await settings.SaveCommand.ExecuteAsync(null);
            settings.SaveCommand.Execute(null);
            await settings.DeleteCommand.ExecuteAsync(null);
            await settings.RefreshCommand.ExecuteAsync(null);
            Assert.Equal(2, fixture.Repository.SaveCalls);
            Assert.Equal(0, fixture.Repository.DeleteCalls);
            Assert.True(fixture.Repository.SaveToken.CanBeCanceled);
            Assert.False(fixture.Repository.SaveToken.IsCancellationRequested);
        }
        finally { gate.SetResult(); await saving; }
        Assert.False(settings.IsBusy);
        Assert.Equal("updated prompt", Assert.Single(fixture.Repository.Profiles).SystemPrompt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandCancellation_ReachesStorageAndPreservesTheEditor(bool delete)
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        if (delete) await settings.SaveCommand.ExecuteAsync(null);
        fixture.Repository.BeforeSave = token => Task.Delay(Timeout.Infinite, token);
        fixture.Repository.BeforeDelete = token => Task.Delay(Timeout.Infinite, token);
        var command = delete ? settings.DeleteCommand : settings.SaveCommand;
        var running = command.ExecuteAsync(null);
        var token = delete ? fixture.Repository.DeleteToken : fixture.Repository.SaveToken;
        Assert.True(token.CanBeCanceled);
        command.Cancel();
        await running;

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(delete ? 1 : 0, fixture.Repository.Profiles.Count);
        Assert.Equal("original", settings.Name);
        Assert.Equal("original prompt", settings.SystemPrompt);
        Assert.True(settings.HasStatusError);
        Assert.Contains("取消", settings.Status);
        fixture.Repository.BeforeSave = null;
        fixture.Repository.BeforeDelete = null;
        await command.ExecuteAsync(null);
        Assert.False(settings.HasStatusError);
        Assert.Equal(delete ? 0 : 1, fixture.Repository.Profiles.Count);
    }

    [Fact]
    public async Task Disposal_CancelsStorageAndRejectsALateNoncooperativeResult()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.Dispose();
        Assert.True(fixture.Repository.SaveToken.IsCancellationRequested);
        gate.SetResult();
        await saving;

        Assert.Single(fixture.Repository.Profiles);
        Assert.Empty(settings.Items);
        Assert.Null(settings.SelectedProfile);
        Assert.Equal(string.Empty, settings.Status);
        Assert.Equal("original", settings.Name);
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.NewCommand.CanExecute(null));
        settings.NewCommand.Execute(null);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.Equal("original", settings.Name);
    }

    [Fact]
    public async Task Save_UsesTheClickedSnapshotRatherThanFieldsChangedDuringModelReload()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Configurations.ModelLoader = () => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.Name = "newer name";
        settings.ShareContext = false;
        settings.ReasoningEffort = "high";
        gate.SetResult([fixture.Model]);
        await saving;

        var saved = Assert.Single(fixture.Repository.Profiles);
        Assert.Equal("original", saved.Name);
        Assert.True(saved.ShareContext);
        Assert.Equal("medium", saved.ReasoningEffort);
        Assert.Equal("newer name", settings.Name);
        Assert.False(settings.ShareContext);
        Assert.Equal("high", settings.ReasoningEffort);
        Assert.Contains("尚未保存", settings.Status);
    }

    [Fact]
    public async Task Save_CannotResurrectADeletedTargetAfterTheEditorContextChanges()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        var original = settings.SelectedProfile!;
        var gate = new TaskCompletionSource<IReadOnlyList<SubagentProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Loader = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "different draft";
        settings.TriggerCondition = "different trigger";
        settings.SystemPrompt = "different prompt";
        await fixture.Repository.DeleteAsync(original.Id, TestContext.Current.CancellationToken);
        gate.SetResult([]);
        await saving;

        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.Empty(fixture.Repository.Profiles);
        Assert.Null(settings.SelectedProfile);
        Assert.Equal("different draft", settings.Name);
        Assert.Equal(string.Empty, settings.Status);
    }

    [Fact]
    public async Task FailedRefresh_PreservesRowsAndDraftAndRequiresASuccessfulRetry()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        settings.SystemPrompt = "newer draft";
        fixture.Repository.Loader = _ => Task.FromException<IReadOnlyList<SubagentProfile>>(
            new InvalidOperationException("fixture-secret-error"));
        await settings.RefreshCommand.ExecuteAsync(null);

        Assert.Single(settings.Items);
        Assert.Equal("newer draft", settings.SystemPrompt);
        Assert.True(settings.HasStatusError);
        Assert.DoesNotContain("fixture-secret-error", settings.Status);
        Assert.False(settings.SaveCommand.CanExecute(null));
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        fixture.Repository.Loader = null;
        await settings.RefreshCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError);
        Assert.True(settings.SaveCommand.CanExecute(null));
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal("newer draft", Assert.Single(fixture.Repository.Profiles).SystemPrompt);
    }

    [Fact]
    public async Task NoncooperativeRead_HasABoundedWaitAndCannotPublishAfterTimeout()
    {
        using var fixture = new SubagentEditorTestFixture(TimeSpan.FromMilliseconds(80));
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource<IReadOnlyList<SubagentProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Loader = _ => gate.Task;
        await settings.RefreshCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(settings.IsLoading);
        Assert.Contains("超时", settings.Status);
        Assert.Equal("original prompt", settings.SystemPrompt);
        gate.SetResult([new SubagentProfile(Guid.NewGuid(), "late", "late", "late", fixture.Model.ProviderId,
            fixture.Model.Id, "high", true, true)]);
        Assert.Empty(settings.Items);
        Assert.False(settings.SaveCommand.CanExecute(null));
    }
}
