using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class MemoryRepository(AppDbContext db) : IMemoryRepository
{
    public async Task<List<MemoryItem>> GetAllAsync(MemoryScope? scope = null, CancellationToken ct = default)
    {
        var query = db.Memories.AsNoTracking();
        if (scope.HasValue)
        {
            query = query.Where(m => m.Scope == scope.Value);
        }
        return await query.OrderByDescending(m => m.UpdatedAt).ToListAsync(ct);
    }

    public Task<MemoryItem?> GetAsync(int id, CancellationToken ct = default) =>
        db.Memories.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<List<MemoryItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length == 0)
        {
            return [];
        }
        return await db.Memories.AsNoTracking()
            .Where(m => m.Title.Contains(term) || m.Content.Contains(term) || (m.Tags != null && m.Tags.Contains(term)))
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(MemoryItem item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTime.UtcNow;
        db.Memories.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MemoryItem item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTime.UtcNow;
        db.Memories.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.Memories.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return;
        db.Memories.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}
