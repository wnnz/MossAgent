using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelReasoningEditorTests
{
    [Theory]
    [InlineData(false, ProviderProtocol.OpenAiResponses)]
    [InlineData(true, ProviderProtocol.OpenAiResponses)]
    [InlineData(false, ProviderProtocol.OpenAiCompatible)]
    [InlineData(true, ProviderProtocol.OpenAiCompatible)]
    public async Task AgentEditors_NormalizeTypedCompatibleEffortAndRestoreIt(bool child, ProviderProtocol protocol)
    {
        var (repository, provider, _) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = protocol };
        if (child)
        {
            var storage = new TestSubagentConfigurationRepository();
            using var editor = new SubagentSettingsViewModel(storage, repository);
            await editor.RefreshCommand.ExecuteAsync(null);
            editor.NewCommand.Execute(null);
            editor.Name = "child";
            editor.TriggerCondition = "review";
            editor.SystemPrompt = "synthetic";
            editor.ReasoningEffort = " VENDOR-Extreme_2 ";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Equal("vendor-extreme_2", Assert.Single(storage.Profiles).ReasoningEffort);
            editor.NewCommand.Execute(null);
            editor.SelectedProfile = Assert.Single(editor.Items);
            Assert.Equal("vendor-extreme_2", editor.ReasoningEffort);
        }
        else
        {
            var storage = new TestAgentConfigurationRepository();
            using var editor = new AgentSettingsViewModel(storage, repository);
            await editor.RefreshCommand.ExecuteAsync(null);
            editor.NewCommand.Execute(null);
            editor.Name = "parent";
            editor.SystemPrompt = "synthetic";
            editor.ReasoningEffort = " VENDOR-Extreme_2 ";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Equal("vendor-extreme_2", Assert.Single(storage.Profiles).ReasoningEffort);
            editor.NewCommand.Execute(null);
            editor.SelectedProfile = Assert.Single(editor.Items);
            Assert.Equal("vendor-extreme_2", editor.ReasoningEffort);
        }
    }

    [Fact]
    public async Task ModelEditor_RejectsLegacyInvalidEffortAndPreservesSavedValuesUntilExplicitCorrection()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.GeminiGenerateContent };
        repository.Models[0] = model with { ModelId = "gemini-3-pro-preview", ReasoningEffort = "medium" };
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        editor.EditProviderModelCommand.Execute(repository.Models[0]);

        Assert.True(editor.HasReasoningError);
        Assert.True(editor.HasEditorNotice);
        Assert.Equal(new[] { "low", "high", "medium" }, editor.ReasoningEfforts);
        editor.ReasoningEffort = null!;
        Assert.Equal("medium", editor.ReasoningEffort);
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.True(editor.HasStatusError);
        Assert.Equal("medium", Assert.Single(repository.Models).ReasoningEffort);
        Assert.True(editor.IsFormOpen);

        editor.ReasoningEffort = "high";
        Assert.False(editor.HasReasoningError);
        Assert.Contains("high", editor.ReasoningEfforts);
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        Assert.Equal("high", Assert.Single(repository.Models).ReasoningEffort);
    }

    [Fact]
    public async Task Discovery_UsesSupportedDefaultsWithoutRewritingExistingModel()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.GeminiGenerateContent };
        repository.Models[0] = model with { ModelId = "gemini-3-pro-preview", ReasoningEffort = "medium" };
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([
            new("gemini-3-pro-preview", "Existing"), new("gemini-3.1-flash-lite-image", "Image"),
            new("gemini-3.8-flash", "Flash"), new("custom-alias", "Custom")]), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        await editor.FetchModelsCommand.ExecuteAsync(null);

        Assert.False(editor.HasStatusError, editor.Status);
        Assert.Equal("medium", repository.Models.Single(item => item.Id == model.Id).ReasoningEffort);
        Assert.Equal("high", repository.Models.Single(item => item.ModelId == "gemini-3.1-flash-lite-image").ReasoningEffort);
        Assert.Equal("medium", repository.Models.Single(item => item.ModelId == "gemini-3.8-flash").ReasoningEffort);
        Assert.Equal("none", repository.Models.Single(item => item.ModelId == "custom-alias").ReasoningEffort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgentEditors_RejectUnsupportedEffortWithoutSavingAndAllowCorrection(bool child)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.GeminiGenerateContent };
        repository.Models[0] = model with { ModelId = "gemini-3.8-flash" };
        if (child)
        {
            var storage = new TestSubagentConfigurationRepository();
            using var editor = new SubagentSettingsViewModel(storage, repository);
            await editor.RefreshCommand.ExecuteAsync(null);
            editor.NewCommand.Execute(null);
            editor.Name = "child";
            editor.TriggerCondition = "review";
            editor.SystemPrompt = "synthetic";
            editor.ReasoningEffort = "minimal";
            Assert.True(editor.HasReasoningError);
            Assert.True(editor.HasEditorNotice);
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Empty(storage.Profiles);
            editor.ReasoningEffort = "high";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Equal("high", Assert.Single(storage.Profiles).ReasoningEffort);
        }
        else
        {
            var storage = new TestAgentConfigurationRepository();
            using var editor = new AgentSettingsViewModel(storage, repository);
            await editor.RefreshCommand.ExecuteAsync(null);
            editor.NewCommand.Execute(null);
            editor.Name = "parent";
            editor.SystemPrompt = "synthetic";
            editor.ReasoningEffort = "minimal";
            Assert.True(editor.HasReasoningError);
            Assert.True(editor.HasEditorNotice);
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Empty(storage.Profiles);
            editor.ReasoningEffort = "high";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Equal("high", Assert.Single(storage.Profiles).ReasoningEffort);
        }
    }
}
