using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class EnabledModelCatalog(IConfigurationRepository configurations)
{
    public Task<IReadOnlyList<ModelPickerItemViewModel>> ReadAsync(CancellationToken token) =>
        ReadAsync(static (provider, model) => new ModelPickerItemViewModel(provider, model), token);

    public async Task<AgentModelOption[]> ReadAgentOptionsAsync(CancellationToken token) =>
        (await ReadAsync(static (provider, model) => new AgentModelOption(model, provider.Name, provider.Protocol), token))
            .OrderBy(static option => option.Label).ToArray();

    private async Task<IReadOnlyList<T>> ReadAsync<T>(Func<AiProvider, ModelProfile, T> createItem, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var items = new List<T>();
        var providers = await configurations.GetProvidersAsync(token).WaitAsync(token);
        for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
        {
            var provider = providers[providerIndex];
            if (!provider.IsEnabled) continue;
            token.ThrowIfCancellationRequested();
            var models = await configurations.GetModelsAsync(provider.Id, token).WaitAsync(token);
            for (var modelIndex = 0; modelIndex < models.Count; modelIndex++)
            {
                var model = models[modelIndex];
                if (model.IsEnabled && model.ProviderId == provider.Id) items.Add(createItem(provider, model));
            }
        }
        token.ThrowIfCancellationRequested();
        return items;
    }
}
