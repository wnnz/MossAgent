using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface IProviderRepository
{
    Task<List<Provider>> GetAllAsync(CancellationToken ct = default);
    Task<Provider?> GetAsync(int id, CancellationToken ct = default);
    Task<Provider?> GetWithModelsAsync(int id, CancellationToken ct = default);
    Task AddAsync(Provider provider, CancellationToken ct = default);
    Task UpdateAsync(Provider provider, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task AddModelAsync(ProviderModel model, CancellationToken ct = default);
    Task UpdateModelAsync(ProviderModel model, CancellationToken ct = default);
    Task DeleteModelAsync(int providerId, int modelId, CancellationToken ct = default);
    Task<ProviderModel?> GetModelAsync(int providerId, int modelId, CancellationToken ct = default);
    Task ReplaceModelsAsync(int providerId, IEnumerable<ProviderModel> models, CancellationToken ct = default);
}
