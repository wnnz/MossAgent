using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class SettingsRepository(AppDbContext db) : ISettingsRepository
{
    public async Task<List<AppSetting>> GetAllAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct);

    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        db.Settings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            db.Settings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await db.SaveChangesAsync(ct);
    }
}
