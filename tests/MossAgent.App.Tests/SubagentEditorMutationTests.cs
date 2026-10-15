using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentEditorMutationTests
{
    [Fact]
    public async Task UnchangedRefresh_KeepsCollectionItemsAndSelectionInstances()
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        var profile = settings.SelectedProfile;
        var model = settings.SelectedModel;
        var changes = 0;
        settings.Items.CollectionChanged += (_, _) => changes++;
        settings.Models.CollectionChanged += (_, _) => changes++;

        await settings.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, changes);
        Assert.Same(profile, settings.SelectedProfile);
        Assert.Same(model, settings.SelectedModel);
    }

    [Fact]
    public async Task Save_PreservesNewerDraftAndKeepsItsSavedIdentity()
    {
        var (settings, repository, _) = Create();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.Name = "newer draft";
        settings.TriggerCondition = "newer trigger";
        settings.SystemPrompt = "newer prompt";
        settings.ReasoningEffort = "high";
        settings.ShareContext = false;
        gate.SetResult();
        await saving;

        var saved = Assert.Single(repository.Profiles);
        Assert.Equal("original", saved.Name);
        Assert.Equal("newer draft", settings.Name);
        Assert.Equal("newer trigger", settings.TriggerCondition);
        Assert.Equal("newer prompt", settings.SystemPrompt);
        Assert.Equal("high", settings.ReasoningEffort);
        Assert.False(settings.ShareContext);
        Assert.Equal(saved.Id, settings.SelectedProfile?.Id);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal("newer draft", Assert.Single(repository.Profiles).Name);
    }

    [Fact]
    public async Task Save_CannotReplaceANewEditorContext()
    {
        var (settings, repository, _) = Create();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeSave = _ => gate.Task;
        var saving = settings.SaveCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "different context";
        settings.TriggerCondition = "different trigger";
        settings.SystemPrompt = "different prompt";
        gate.SetResult();
        await saving;

        Assert.Equal("original", Assert.Single(repository.Profiles).Name);
        Assert.Null(settings.SelectedProfile);
        Assert.Equal("different context", settings.Name);
        Assert.Equal(string.Empty, settings.Status);
    }

    [Fact]
    public async Task Refresh_PreservesUnsavedFieldsAndTheSelectedModelIdentity()
    {
        var (settings, repository, configurations) = Create();
        await settings.SaveCommand.ExecuteAsync(null);
        settings.Name = "unsaved name";
        settings.SystemPrompt = "unsaved prompt";
        var model = settings.SelectedModel!.Model;
        configurations.ModelLoader = () => Task.FromResult<IReadOnlyList<ModelProfile>>(
            [model with { DisplayName = "renamed model" }]);

        await settings.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("unsaved name", settings.Name);
        Assert.Equal("unsaved prompt", settings.SystemPrompt);
        Assert.Equal(model.Id, settings.SelectedModel?.Model.Id);
        Assert.Equal("renamed model", settings.SelectedModel?.Model.DisplayName);
        Assert.Equal("original", Assert.Single(repository.Profiles).Name);
    }

    [Fact]
    public async Task Delete_CannotResetAnotherSelectedProfileOrItsDraft()
    {
        var (settings, repository, _) = Create();
        await settings.SaveCommand.ExecuteAsync(null);
        var original = settings.SelectedProfile!;
        settings.NewCommand.Execute(null);
        settings.Name = "second";
        settings.TriggerCondition = "second trigger";
        settings.SystemPrompt = "second prompt";
        await settings.SaveCommand.ExecuteAsync(null);
        var second = settings.SelectedProfile!;
        settings.SelectedProfile = original;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeDelete = _ => gate.Task;
        var deleting = settings.DeleteCommand.ExecuteAsync(null);
        settings.SelectedProfile = second;
        settings.SystemPrompt = "new second prompt";
        gate.SetResult();
        await deleting;

        Assert.Equal(second.Id, Assert.Single(repository.Profiles).Id);
        Assert.Equal(second.Id, settings.SelectedProfile?.Id);
        Assert.Equal("new second prompt", settings.SystemPrompt);
        Assert.Equal(string.Empty, settings.Status);
    }

    private static (SubagentSettingsViewModel Settings, TestSubagentConfigurationRepository Repository,
        TestSubagentModelConfigurationRepository Configurations) Create()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture-key", null, false, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "child", "child", 16000, 2000,
            "medium", null, null, false, false, false, true, "{}");
        var repository = new TestSubagentConfigurationRepository();
        var configurations = new TestSubagentModelConfigurationRepository(provider, model);
        var settings = new SubagentSettingsViewModel(repository, configurations)
        {
            Name = "original",
            TriggerCondition = "original trigger",
            SystemPrompt = "original prompt"
        };
        return (settings, repository, configurations);
    }
}
