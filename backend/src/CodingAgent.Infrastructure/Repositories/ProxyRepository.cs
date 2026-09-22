using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class ProxyRepository(AppDbContext db) : IProxyRepository
{
    public async Task<List<ProxyServer>> GetAllAsync(CancellationToken ct = default) =>
        await db.Proxies.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public Task<ProxyServer?> GetAsync(int id, CancellationToken ct = default) =>
        db.Proxies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(ProxyServer proxy, CancellationToken ct = default)
    {
        db.Proxies.Add(proxy);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProxyServer proxy, CancellationToken ct = default)
    {
        db.Proxies.Update(proxy);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var proxy = await db.Proxies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proxy is null) return;
        db.Proxies.Remove(proxy);
        await db.SaveChangesAsync(ct);
    }
}
