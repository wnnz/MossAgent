using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class SubAgentRepository(AppDbContext db) : ISubAgentRepository
{
    public async Task<List<SubAgent>> GetAllAsync(bool enabledOnly = false, CancellationToken ct = default)
    {
        var query = db.SubAgents.AsNoTracking();
        if (enabledOnly)
        {
            query = query.Where(s => s.Enabled);
        }
        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public Task<SubAgent?> GetAsync(int id, CancellationToken ct = default) =>
        db.SubAgents.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(SubAgent subAgent, CancellationToken ct = default)
    {
        db.SubAgents.Add(subAgent);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SubAgent subAgent, CancellationToken ct = default)
    {
        db.SubAgents.Update(subAgent);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var subAgent = await db.SubAgents.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subAgent is null) return;
        db.SubAgents.Remove(subAgent);
        await db.SaveChangesAsync(ct);
    }
}
