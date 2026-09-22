using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Interfaces;

public interface IMemoryRepository
{
    Task<List<MemoryItem>> GetAllAsync(MemoryScope? scope = null, CancellationToken ct = default);
    Task<MemoryItem?> GetAsync(int id, CancellationToken ct = default);
    Task<List<MemoryItem>> SearchAsync(string query, CancellationToken ct = default);
    Task AddAsync(MemoryItem item, CancellationToken ct = default);
    Task UpdateAsync(MemoryItem item, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
