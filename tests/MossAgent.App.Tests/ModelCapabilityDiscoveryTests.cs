using MossAgent.App.ViewModels;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelCapabilityDiscoveryTests
{
    [Fact]
    public async Task Fetch_FillsNewModelsAndNeverReplacesUserSettings()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.OpenAiCompatible };
        var original = model with
        {
            ModelId = "gpt-5",
            ContextLength = 32000,
            ReasoningEffort = "vendor-extreme",
            SupportsImages = false,
            SupportsFiles = true,
            AdvancedParametersJson = "{\"vendor\":true}"
        };
        repository.Models[0] = original;
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([
            new("gpt-5", "Keep"), new("claude-opus-4-6", "Claude"), new("unknown-vision-thinking", "Unknown")]), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        await editor.FetchModelsCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        Assert.Equal(original, repository.Models.Single(item => item.ModelId == "gpt-5"));
        var claude = repository.Models.Single(item => item.ModelId == "claude-opus-4-6");
        Assert.Equal(1_000_000, claude.ContextLength);
        Assert.Equal(128_000, claude.MaximumOutputTokens);
        Assert.True(claude.SupportsImages);
        Assert.True(claude.SupportsFiles);
        Assert.Equal("medium", claude.ReasoningEffort);
        Assert.Equal("{}", claude.AdvancedParametersJson);
        var unknown = repository.Models.Single(item => item.ModelId == "unknown-vision-thinking");
        Assert.Equal(128_000, unknown.ContextLength);
        Assert.Equal(8192, unknown.MaximumOutputTokens);
        Assert.False(unknown.SupportsImages);
        Assert.False(unknown.SupportsFiles);
        editor.EditProviderModelCommand.Execute(claude);
        Assert.Equal(new[] { "low", "medium", "high", "max" }, editor.ReasoningEfforts);
        editor.ReasoningEffort = "vendor-extreme";
        await editor.SaveModelCommand.ExecuteAsync(null);
        var customized = repository.Models.Single(item => item.Id == claude.Id);
        await editor.FetchModelsCommand.ExecuteAsync(null);
        Assert.Equal(customized, repository.Models.Single(item => item.Id == claude.Id));
        editor.EditProviderModelCommand.Execute(customized);
        Assert.Equal("vendor-extreme", editor.ReasoningEffort);
        Assert.False(editor.HasReasoningError);
        Assert.Equal(3, repository.Models.Count);
    }

    [Fact]
    public async Task Fetch_ImportedLimitsStayValidWhenSourceOutputEqualsContext()
    {
        var (repository, provider, _) = ModelPickerReloadTests.Profiles();
        repository.Models.Clear();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.OpenAiCompatible };
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService(
            ModelCapabilityCatalog.Data.Models.Select(item => new DiscoveredModel(item.Provider + "/" + item.ModelId, item.ModelId)).ToArray()), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        await editor.FetchModelsCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        Assert.Equal(ModelCapabilityCatalog.Data.Models.Length, repository.Models.Count);
        foreach (var model in repository.Models.ToArray())
        {
            editor.EditProviderModelCommand.Execute(model);
            await editor.SaveModelCommand.ExecuteAsync(null);
            Assert.False(editor.HasStatusError, editor.Status);
            Assert.False(editor.IsFormOpen);
        }
    }
}
