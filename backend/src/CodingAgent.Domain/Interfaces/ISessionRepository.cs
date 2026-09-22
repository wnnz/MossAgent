using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Interfaces;

public interface ISessionRepository
{
    Task<List<Session>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<Session?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default);
    Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task DeleteMessagesAsync(Guid sessionId, CancellationToken ct = default);
}
