using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class ProviderCatalogLookupTests
{
    [Fact]
    public void Lookups_TrackCollectionChangesWithoutCachingOrFallingBackToDuplicateGroups()
    {
        using var catalog = new ProviderModelCatalogViewModel(new ModelPickerProbeRepository());
        var provider = ProviderSettingsRowTestData.Provider();
        var duplicate = provider with { Name = "duplicate" };
        var firstGroup = new ProviderModelGroupViewModel(provider);
        var secondGroup = new ProviderModelGroupViewModel(duplicate);
        var model = ProviderSettingsRowTestData.Model(provider.Id);
        var duplicateModel = model with { DisplayName = "duplicate" };
        firstGroup.Models.ReplaceWith([model, duplicateModel]);
        var secondModel = model with { Id = Guid.NewGuid() };
        secondGroup.Models.Add(secondModel);
        catalog.Providers.ReplaceWith([provider, duplicate]);
        catalog.Groups.ReplaceWith([firstGroup, secondGroup]);

        Assert.Null(catalog.FindProvider(null));
        Assert.Null(catalog.FindGroup(null));
        Assert.Null(catalog.FindProvider(Guid.NewGuid()));
        Assert.Same(provider, catalog.FindProvider(provider.Id));
        Assert.Same(firstGroup, catalog.FindGroup(provider.Id));
        Assert.Same(model, catalog.FindModel(provider.Id, model.Id));
        Assert.Null(catalog.FindModel(provider.Id, secondModel.Id));
        firstGroup.Models.Move(0, 1);
        Assert.Same(duplicateModel, catalog.FindModel(provider.Id, model.Id));
        catalog.Providers.Move(0, 1);
        catalog.Groups.Move(0, 1);
        Assert.Same(duplicate, catalog.FindProvider(provider.Id));
        Assert.Same(secondGroup, catalog.FindGroup(provider.Id));
        Assert.Same(secondModel, catalog.FindModel(provider.Id, secondModel.Id));
        Assert.Null(catalog.FindModel(provider.Id, model.Id));
        catalog.Providers.Clear();
        catalog.Groups.Clear();
        Assert.Null(catalog.FindProvider(provider.Id));
        Assert.Null(catalog.FindGroup(provider.Id));
        Assert.Null(catalog.FindModel(provider.Id, model.Id));
    }

    [Fact]
    public void StaleRows_ResolveFirstCurrentOccurrenceWithoutReorderingTheCatalog()
    {
        var repository = Repository();
        var provider = repository.Providers[1];
        var model = repository.Models[1];
        repository.Providers.Add(provider with { Name = "duplicate provider" });
        repository.Models.Add(model with { DisplayName = "duplicate model" });
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);

        providers.EditProviderCommand.Execute(provider with { Name = "stale provider" });
        models.AddModelForProviderCommand.Execute(models.ProviderGroups[2]);
        models.EditProviderModelCommand.Execute(model with { DisplayName = "stale model" });

        Assert.Same(provider, providers.SelectedItem);
        Assert.Same(provider, models.SelectedProvider);
        Assert.Same(model, models.SelectedModel);
        Assert.Equal(model.DisplayName, models.DisplayName);
        Assert.Equal(repository.Providers, providers.Items);
        Assert.Same(model, models.Models[0]);
        Assert.Equal("duplicate model", models.Models[1].DisplayName);
    }

    [Fact]
    public void IdenticalModelIds_StillRequireTheOwningProvider()
    {
        var repository = Repository();
        repository.Models[1] = repository.Models[1] with { Id = repository.Models[0].Id };
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        models.EditProviderModelCommand.Execute(repository.Models[1]);

        Assert.Same(repository.Providers[1], models.SelectedProvider);
        Assert.Same(repository.Models[1], models.SelectedModel);
        var foreign = repository.Models[1] with { ProviderId = Guid.NewGuid() };
        Assert.False(models.EditProviderModelCommand.CanExecute(foreign));
        Assert.False(models.DeleteProviderModelCommand.CanExecute(foreign));
        models.EditProviderModelCommand.Execute(foreign);
        Assert.Same(repository.Models[1], models.SelectedModel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmProviderCommandAndAvailabilityChecks_DoNotAllocate(bool missing)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var target = repository.Providers[1];
        if (missing) target = target with { Id = Guid.NewGuid() };
        providers.SelectedItem = target;
        var valid = true;
        for (var index = 0; index < 1_000; index++) valid &= CheckProvider(providers, target, missing);
        var allocated = AllocationMeasurement.Measure(() =>
        {
            for (var index = 0; index < 10_000; index++) valid &= CheckProvider(providers, target, missing);
        });
        Assert.True(valid);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmModelCommandAndAvailabilityChecks_DoNotAllocate(bool missing)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var provider = repository.Providers[1];
        var target = repository.Models[1];
        var group = models.ProviderGroups[1];
        if (missing)
        {
            provider = provider with { Id = Guid.NewGuid() };
            target = target with { ProviderId = provider.Id };
            group = new ProviderModelGroupViewModel(provider);
        }
        models.SelectedProvider = provider;
        models.SelectedModel = target;
        var valid = true;
        for (var index = 0; index < 1_000; index++) valid &= CheckModel(models, target, group, missing);
        var allocated = AllocationMeasurement.Measure(() =>
        {
            for (var index = 0; index < 10_000; index++) valid &= CheckModel(models, target, group, missing);
        });
        Assert.True(valid);
        Assert.Equal(0, allocated);
    }

    private static bool CheckProvider(ProviderSettingsViewModel providers, AiProvider target, bool missing) =>
        providers.EditProviderCommand.CanExecute(target) == !missing
        && providers.SaveCommand.CanExecute(null) == !missing
        && providers.DeleteCommand.CanExecute(null) == !missing
        && providers.IsEditorTargetUnavailable == missing;

    private static bool CheckModel(ModelSettingsViewModel models, ModelProfile target,
        ProviderModelGroupViewModel group, bool missing) =>
        models.EditProviderModelCommand.CanExecute(target) == !missing
        && models.DeleteProviderModelCommand.CanExecute(target) == !missing
        && models.FetchModelsForProviderCommand.CanExecute(group) == !missing
        && models.AddModelForProviderCommand.CanExecute(group) == !missing
        && models.SaveModelCommand.CanExecute(null) == !missing
        && models.DeleteModelCommand.CanExecute(null) == !missing
        && models.FetchModelsCommand.CanExecute(null) == !missing
        && models.IsEditorTargetUnavailable == missing;

    private static ModelPickerProbeRepository Repository()
    {
        var repository = new ModelPickerProbeRepository();
        repository.Providers.AddRange([ProviderSettingsRowTestData.Provider(), ProviderSettingsRowTestData.Provider(false)]);
        foreach (var provider in repository.Providers)
        {
            repository.Models.Add(ProviderSettingsRowTestData.Model(provider.Id));
        }
        return repository;
    }
}
