using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class SessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task<List<Session>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Sessions.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(s => s.Status == SessionStatus.Active);
        }
        return await query.OrderByDescending(s => s.UpdatedAt).ToListAsync(ct);
    }

    public Task<Session?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Session session, CancellationToken ct = default)
    {
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        db.Sessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return;
        db.Sessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default) =>
        await db.Messages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

    public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    public async Task DeleteMessagesAsync(Guid sessionId, CancellationToken ct = default)
    {
        await db.Messages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync(ct);
    }
}
