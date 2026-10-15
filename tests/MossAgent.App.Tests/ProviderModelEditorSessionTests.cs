using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderModelEditorSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReopeningProviderAfterCancelReloadsSavedFieldsAndClearsValidation(bool selectedEntry)
    {
        var repository = Repository();
        var original = repository.Store.Providers[0];
        using var providers = new ProviderSettingsViewModel(repository);
        OpenProvider(providers, original, selectedEntry);
        providers.Name = "cancelled draft";
        providers.BaseUrl = "not-a-url";
        providers.ApiKey = "cancelled-fixture-key";
        providers.Protocol = ProviderProtocol.GeminiGenerateContent;
        providers.IsDefault = !original.IsDefault;
        providers.IsEnabled = !original.IsEnabled;
        providers.HeadersJson = "{\"draft\":\"value\"}";
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.True(providers.HasValidationError);
        providers.CloseFormCommand.Execute(null);

        OpenProvider(providers, original, selectedEntry);

        Assert.True(providers.IsFormOpen);
        Assert.Equal(original.Name, providers.Name);
        Assert.Equal(original.BaseUri.ToString(), providers.BaseUrl);
        Assert.Empty(providers.ApiKey);
        Assert.Equal(original.Protocol, providers.Protocol);
        Assert.Equal(original.IsDefault, providers.IsDefault);
        Assert.Equal(original.IsEnabled, providers.IsEnabled);
        Assert.Equal("{}", providers.HeadersJson);
        Assert.False(providers.HasValidationError);
        Assert.DoesNotContain("cancelled-fixture-key", providers.Status);
        Assert.Empty(repository.Writes);
        Assert.Same(original, Assert.Single(repository.Store.Providers));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReopeningModelAfterCancelReloadsSavedParametersAndClearsValidation(bool selectedEntry)
    {
        var repository = Repository();
        var original = repository.Store.Models[0];
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenModel(models, original, selectedEntry);
        models.ModelId = string.Empty;
        models.DisplayName = "cancelled draft";
        models.ContextLength = 256000;
        models.MaximumOutputTokens = 4096;
        models.ReasoningEffort = "high";
        models.Temperature = 0.7;
        models.TopP = 0.8;
        models.IsDefault = !original.IsDefault;
        models.IsEnabled = !original.IsEnabled;
        models.SupportsImages = !original.SupportsImages;
        models.SupportsFiles = !original.SupportsFiles;
        models.AdvancedJson = "{\"draft\":true}";
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.NotEmpty(models.EditorStatus);
        models.CloseFormCommand.Execute(null);

        OpenModel(models, original, selectedEntry);

        Assert.True(models.IsFormOpen);
        Assert.Equal(original.ModelId, models.ModelId);
        Assert.Equal(original.DisplayName, models.DisplayName);
        Assert.Equal(original.ContextLength, models.ContextLength);
        Assert.Equal(original.MaximumOutputTokens, models.MaximumOutputTokens);
        Assert.Equal(original.ReasoningEffort, models.ReasoningEffort);
        Assert.Equal(original.Temperature, models.Temperature);
        Assert.Equal(original.TopP, models.TopP);
        Assert.Equal(original.IsDefault, models.IsDefault);
        Assert.Equal(original.IsEnabled, models.IsEnabled);
        Assert.Equal(original.SupportsImages, models.SupportsImages);
        Assert.Equal(original.SupportsFiles, models.SupportsFiles);
        Assert.Equal(original.AdvancedParametersJson, models.AdvancedJson);
        Assert.Empty(models.EditorStatus);
        Assert.Empty(repository.Writes);
        Assert.Same(original, Assert.Single(repository.Store.Models));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvokingAnAlreadyOpenProviderKeepsItsCurrentDraft(bool selectedEntry)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var original = repository.Store.Providers[0];
        OpenProvider(providers, original, selectedEntry);
        providers.Name = "retained draft";
        providers.BaseUrl = "https://draft.example.test/v1/";

        OpenProvider(providers, original, selectedEntry);

        Assert.Equal("retained draft", providers.Name);
        Assert.Equal("https://draft.example.test/v1/", providers.BaseUrl);
        Assert.Empty(repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvokingAnAlreadyOpenModelKeepsItsCurrentDraft(bool selectedEntry)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var original = repository.Store.Models[0];
        OpenModel(models, original, selectedEntry);
        models.DisplayName = "retained draft";
        models.ContextLength = 256000;

        OpenModel(models, original, selectedEntry);

        Assert.Equal("retained draft", models.DisplayName);
        Assert.Equal(256000, models.ContextLength);
        Assert.Empty(repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDisposedProviderCannotStartANewEditorSession(bool disposed)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        providers.SelectedItem = repository.Store.Providers[0];
        if (disposed) providers.Dispose();
        else providers.Catalog.Providers.Clear();

        providers.OpenEditFormCommand.Execute(null);

        Assert.False(providers.IsFormOpen);
        Assert.Empty(repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDisposedModelCannotStartANewEditorSession(bool disposed)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        models.SelectedModel = repository.Store.Models[0];
        if (disposed) models.Dispose();
        else models.ProviderGroups.Clear();

        models.OpenEditFormCommand.Execute(null);

        Assert.False(models.IsFormOpen);
        Assert.Empty(repository.Writes);
    }

    private static void OpenProvider(ProviderSettingsViewModel providers, AiProvider provider, bool selectedEntry)
    {
        if (selectedEntry)
        {
            providers.SelectedItem = provider;
            providers.OpenEditFormCommand.Execute(null);
        }
        else providers.EditProviderCommand.Execute(provider);
    }

    private static void OpenModel(ModelSettingsViewModel models, ModelProfile model, bool selectedEntry)
    {
        if (selectedEntry)
        {
            models.SelectedModel = model;
            models.OpenEditFormCommand.Execute(null);
        }
        else models.EditProviderModelCommand.Execute(model);
    }

    private static ConfigurationMutationProbeRepository Repository()
    {
        var repository = new ConfigurationMutationProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider() with { ApiKey = "saved-fixture-key" };
        repository.Store.Providers.Add(provider);
        repository.Store.Models.Add(ProviderSettingsRowTestData.Model(provider.Id));
        return repository;
    }
}
