using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AgentEditorMutationTests
{
    [Fact]
    public async Task Save_PreservesNewerDraftAndKeepsItsSavedIdentity()
    {
        using var fixture = new AgentEditorTestFixture();
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.Name = "newer draft";
        settings.SystemPrompt = "newer prompt";
        settings.ReasoningEffort = "high";
        settings.IsEnabled = false;
        gate.SetResult();
        await saving;

        var saved = Assert.Single(fixture.Repository.Profiles);
        Assert.Equal("original", saved.Name);
        Assert.Equal("newer draft", settings.Name);
        Assert.Equal("newer prompt", settings.SystemPrompt);
        Assert.Equal("high", settings.ReasoningEffort);
        Assert.False(settings.IsEnabled);
        Assert.Equal(saved.Id, settings.SelectedProfile?.Id);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal("newer draft", Assert.Single(fixture.Repository.Profiles).Name);
    }

    [Fact]
    public async Task Save_CannotReplaceANewEditorContext()
    {
        using var fixture = new AgentEditorTestFixture();
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "different context";
        settings.SystemPrompt = "different prompt";
        gate.SetResult();
        await saving;

        Assert.Equal("original", Assert.Single(fixture.Repository.Profiles).Name);
        Assert.Null(settings.SelectedProfile);
        Assert.Equal("different context", settings.Name);
        Assert.Equal(string.Empty, settings.Status);
    }

    [Fact]
    public async Task Refresh_PreservesUnsavedFieldsAndTheSelectedModelIdentity()
    {
        using var fixture = new AgentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        settings.Name = "unsaved name";
        settings.SystemPrompt = "unsaved prompt";
        fixture.Configurations.ModelLoader = () => Task.FromResult<IReadOnlyList<ModelProfile>>(
            [fixture.Model with { DisplayName = "renamed model" }]);

        await settings.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("unsaved name", settings.Name);
        Assert.Equal("unsaved prompt", settings.SystemPrompt);
        Assert.Equal(fixture.Model.Id, settings.SelectedModel?.Model.Id);
        Assert.Equal("renamed model", settings.SelectedModel?.Model.DisplayName);
        Assert.Equal("original", Assert.Single(fixture.Repository.Profiles).Name);
    }

    [Fact]
    public async Task Delete_CannotResetAnotherSelectedProfileOrItsDraft()
    {
        using var fixture = new AgentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        var original = settings.SelectedProfile!;
        settings.NewCommand.Execute(null);
        settings.Name = "second";
        settings.SystemPrompt = "second prompt";
        await settings.SaveCommand.ExecuteAsync(null);
        var second = settings.SelectedProfile!;
        settings.SelectedProfile = original;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeDelete = _ => gate.Task;
        var deleting = settings.DeleteCommand.ExecuteAsync(null);
        settings.SelectedProfile = second;
        settings.SystemPrompt = "new second prompt";
        gate.SetResult();
        await deleting;

        Assert.Equal(second.Id, Assert.Single(fixture.Repository.Profiles).Id);
        Assert.Equal(second.Id, settings.SelectedProfile?.Id);
        Assert.Equal("new second prompt", settings.SystemPrompt);
        Assert.Equal(string.Empty, settings.Status);
    }
}
