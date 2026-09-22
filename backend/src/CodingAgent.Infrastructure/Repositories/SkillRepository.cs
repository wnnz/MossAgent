using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class SkillRepository(AppDbContext db) : ISkillRepository
{
    public async Task<List<Skill>> GetAllAsync(bool enabledOnly = false, CancellationToken ct = default)
    {
        var query = db.Skills.AsNoTracking();
        if (enabledOnly)
        {
            query = query.Where(s => s.Enabled);
        }
        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public Task<Skill?> GetAsync(int id, CancellationToken ct = default) =>
        db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Skill?> GetByNameAsync(string name, CancellationToken ct = default) =>
        db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name, ct);

    public async Task AddAsync(Skill skill, CancellationToken ct = default)
    {
        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Skill skill, CancellationToken ct = default)
    {
        db.Skills.Update(skill);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null) return;
        db.Skills.Remove(skill);
        await db.SaveChangesAsync(ct);
    }
}
