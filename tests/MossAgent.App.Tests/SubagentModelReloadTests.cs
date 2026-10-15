using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentModelReloadTests
{
    [Fact]
    public async Task EarlierReload_CannotOverwriteANewerCatalog()
    {
        var (provider, model) = Profiles();
        var configurations = new TestSubagentModelConfigurationRepository(provider, model);
        var settings = new SubagentSettingsViewModel(new TestSubagentConfigurationRepository(), configurations);
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        configurations.ModelLoader = () => delayed.Task;
        var earlier = settings.ReloadModelsAsync(TestContext.Current.CancellationToken);
        var updated = model with { DisplayName = "newer" };
        configurations.ModelLoader = () => Task.FromResult<IReadOnlyList<ModelProfile>>([updated]);

        Assert.True(await settings.ReloadModelsAsync(TestContext.Current.CancellationToken));
        delayed.SetResult([model]);

        Assert.False(await earlier);
        Assert.Equal(updated, settings.SelectedModel?.Model);
        Assert.Equal(updated, Assert.Single(settings.Models).Model);
    }

    [Fact]
    public async Task UnavailableSelection_IsNotSubstitutedOnAnotherReloadOrSave()
    {
        var (provider, model) = Profiles();
        var configurations = new TestSubagentModelConfigurationRepository(provider, model);
        var repository = new TestSubagentConfigurationRepository();
        var settings = new SubagentSettingsViewModel(repository, configurations);
        settings.Name = "draft";
        settings.TriggerCondition = "review";
        settings.SystemPrompt = "check";
        var alternative = model with { Id = Guid.NewGuid(), ModelId = "alternative" };
        configurations.ModelLoader = () => Task.FromResult<IReadOnlyList<ModelProfile>>([alternative]);

        Assert.True(await settings.ReloadModelsAsync(TestContext.Current.CancellationToken));
        Assert.True(await settings.ReloadModelsAsync(TestContext.Current.CancellationToken));
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.Null(settings.SelectedModel);
        Assert.Empty(repository.Profiles);
        configurations.ModelLoader = () => Task.FromResult<IReadOnlyList<ModelProfile>>([model, alternative]);
        Assert.True(await settings.ReloadModelsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(model, settings.SelectedModel?.Model);
        Assert.Equal("draft", settings.Name);
    }

    [Fact]
    public async Task FailedReload_PreservesSnapshotButDoesNotSaveOrExposeException()
    {
        var (provider, model) = Profiles();
        var configurations = new TestSubagentModelConfigurationRepository(provider, model);
        var repository = new TestSubagentConfigurationRepository();
        var settings = new SubagentSettingsViewModel(repository, configurations);
        settings.Name = "draft";
        settings.TriggerCondition = "review";
        settings.SystemPrompt = "check";
        configurations.ModelLoader = () => Task.FromException<IReadOnlyList<ModelProfile>>(
            new InvalidOperationException("fixture-secret-error"));

        Assert.False(await settings.ReloadModelsAsync(TestContext.Current.CancellationToken));
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.Equal(model, settings.SelectedModel?.Model);
        Assert.Empty(repository.Profiles);
        Assert.Equal("draft", settings.Name);
        Assert.Contains("加载子代理模型失败", settings.Status);
        Assert.DoesNotContain("fixture-secret-error", settings.Status);
    }

    private static (AiProvider Provider, ModelProfile Model) Profiles()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "fixture", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "child", "child", 16384, 2048,
            "medium", null, null, false, false, false, true, "{}");
        return (provider, model);
    }
}
