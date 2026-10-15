using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentSettingsDialogFlowTests
{
    [Fact]
    public async Task CreateEditCancelAndDelete_RequireExplicitActions()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        Assert.False(settings.IsFormOpen);
        Assert.True(settings.HasNoItems);
        settings.OpenCreateFormCommand.Execute(null);
        Assert.True(settings.IsFormOpen);
        Assert.Null(settings.SelectedProfile);
        Assert.Empty(settings.Name);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.True(settings.IsFormOpen);
        Assert.True(settings.HasStatusError);
        Assert.Empty(fixture.Repository.Profiles);
        settings.Name = "created";
        settings.TriggerCondition = "review code";
        settings.SystemPrompt = "original prompt";
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen);
        Assert.False(settings.HasNoItems);
        var profile = Assert.Single(fixture.Repository.Profiles);
        settings.OpenEditFormCommand.Execute(profile);
        Assert.True(settings.IsFormOpen);
        Assert.Equal("created", settings.Name);
        settings.Name = "cancelled draft";
        settings.CloseFormCommand.Execute(null);
        Assert.False(settings.IsFormOpen);
        Assert.Equal("created", Assert.Single(fixture.Repository.Profiles).Name);
        settings.OpenEditFormCommand.Execute(profile);
        Assert.Equal("created", settings.Name);
        settings.Name = "updated";
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen);
        var updated = Assert.Single(fixture.Repository.Profiles);
        Assert.Equal(profile.Id, updated.Id);
        Assert.Equal("updated", updated.Name);
        settings.OpenEditFormCommand.Execute(updated);
        await settings.DeleteCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen);
        Assert.True(settings.HasNoItems);
        Assert.Empty(fixture.Repository.Profiles);
    }

    [Fact]
    public async Task SaveFailure_KeepsDialogAndDraftUntilSuccessfulRetry()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        settings.IsFormOpen = true;
        fixture.Repository.BeforeSave = _ => Task.FromException(new InvalidOperationException("fixture-secret-error"));
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.True(settings.IsFormOpen);
        Assert.True(settings.HasStatusError);
        Assert.DoesNotContain("fixture-secret-error", settings.Status);
        Assert.Equal("original", settings.Name);
        Assert.Equal("original prompt", settings.SystemPrompt);
        fixture.Repository.BeforeSave = null;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen);
        Assert.False(settings.HasStatusError);
        Assert.Single(fixture.Repository.Profiles);
    }

    [Fact]
    public async Task PendingSave_CannotCloseOrReplaceANewerDialog()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        settings.IsFormOpen = true;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.CloseFormCommand.Execute(null);
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "new draft";
        settings.TriggerCondition = "new trigger";
        settings.SystemPrompt = "new prompt";
        gate.SetResult();
        await saving;
        Assert.True(settings.IsFormOpen);
        Assert.Null(settings.SelectedProfile);
        Assert.Equal("new draft", settings.Name);
        Assert.Equal("new prompt", settings.SystemPrompt);
        Assert.Equal("original", Assert.Single(fixture.Repository.Profiles).Name);
        Assert.Empty(settings.Status);
    }

    [Fact]
    public async Task MissingTargetsAndDisposedEditors_CannotBeOpened()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        var removed = Assert.Single(fixture.Repository.Profiles);
        await fixture.Repository.DeleteAsync(removed.Id, TestContext.Current.CancellationToken);
        await settings.RefreshCommand.ExecuteAsync(null);
        Assert.False(settings.OpenEditFormCommand.CanExecute(removed));
        settings.OpenEditFormCommand.Execute(removed);
        Assert.False(settings.IsFormOpen);
        settings.OpenCreateFormCommand.Execute(null);
        settings.CloseFormCommand.Execute(null);
        Assert.Empty(fixture.Repository.Profiles);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        settings.Dispose();
        Assert.False(settings.OpenCreateFormCommand.CanExecute(null));
        Assert.False(settings.OpenEditFormCommand.CanExecute(removed));
        Assert.False(settings.CloseFormCommand.CanExecute(null));
    }
}
