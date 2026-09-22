using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class ProviderRepository(AppDbContext db) : IProviderRepository
{
    public async Task<List<Provider>> GetAllAsync(CancellationToken ct = default) =>
        await db.Providers.AsNoTracking().Include(p => p.Models).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<Provider?> GetAsync(int id, CancellationToken ct = default) =>
        db.Providers.AsNoTracking().Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Provider?> GetWithModelsAsync(int id, CancellationToken ct = default) =>
        db.Providers.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Provider provider, CancellationToken ct = default)
    {
        db.Providers.Add(provider);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Provider provider, CancellationToken ct = default)
    {
        db.Providers.Update(provider);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var provider = await db.Providers.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (provider is null) return;
        db.Providers.Remove(provider);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddModelAsync(ProviderModel model, CancellationToken ct = default)
    {
        db.ProviderModels.Add(model);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateModelAsync(ProviderModel model, CancellationToken ct = default)
    {
        db.ProviderModels.Update(model);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteModelAsync(int providerId, int modelId, CancellationToken ct = default)
    {
        var model = await db.ProviderModels.FirstOrDefaultAsync(m => m.Id == modelId && m.ProviderId == providerId, ct);
        if (model is null) return;
        db.ProviderModels.Remove(model);
        await db.SaveChangesAsync(ct);
    }

    public Task<ProviderModel?> GetModelAsync(int providerId, int modelId, CancellationToken ct = default) =>
        db.ProviderModels.FirstOrDefaultAsync(m => m.Id == modelId && m.ProviderId == providerId, ct);

    public async Task ReplaceModelsAsync(int providerId, IEnumerable<ProviderModel> models, CancellationToken ct = default)
    {
        var existing = await db.ProviderModels.Where(m => m.ProviderId == providerId && !m.IsCustom).ToListAsync(ct);
        db.ProviderModels.RemoveRange(existing);
        db.ProviderModels.AddRange(models);
        await db.SaveChangesAsync(ct);
    }
}
