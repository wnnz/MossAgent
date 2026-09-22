using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<AuthRecord?> GetAsync(CancellationToken ct = default) =>
        db.AuthRecords.AsNoTracking().FirstOrDefaultAsync(ct);

    public async Task AddAsync(AuthRecord record, CancellationToken ct = default)
    {
        db.AuthRecords.Add(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AuthRecord record, CancellationToken ct = default)
    {
        db.AuthRecords.Update(record);
        await db.SaveChangesAsync(ct);
    }
}
