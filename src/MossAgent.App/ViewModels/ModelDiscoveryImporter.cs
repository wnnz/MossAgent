using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ModelDiscoveryImporter(IConfigurationRepository repository, ProviderModelCatalogViewModel catalog)
{
    public Task<bool> ImportAsync(AiProvider provider, IReadOnlyList<DiscoveredModel> discovered,
        Func<AiProvider, DiscoveredModel, ModelProfile> createModel, CancellationToken ownerToken, Action<string> reportFailure) =>
        catalog.Mutations.TryExecuteAsync(async token =>
        {
            RequireCurrentProvider(provider);
            var models = await repository.GetModelsAsync(provider.Id, token).WaitAsync(token);
            var existingIds = models.Where(model => model.ProviderId == provider.Id)
                .Select(model => model.ModelId).ToHashSet(StringComparer.Ordinal);
            foreach (var item in discovered.Where(item => existingIds.Add(item.Id)))
            {
                RequireCurrentProvider(provider);
                await repository.SaveModelAsync(createModel(provider, item), token);
            }
        }, ownerToken, reportFailure, "ModelImportFailed");

    private void RequireCurrentProvider(AiProvider provider)
    {
        if (!catalog.IsAvailable || !catalog.Providers.Any(item => item.Id == provider.Id))
            throw new InvalidOperationException();
    }
}
