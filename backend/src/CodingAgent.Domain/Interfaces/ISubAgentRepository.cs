using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface ISubAgentRepository
{
    Task<List<SubAgent>> GetAllAsync(bool enabledOnly = false, CancellationToken ct = default);
    Task<SubAgent?> GetAsync(int id, CancellationToken ct = default);
    Task AddAsync(SubAgent subAgent, CancellationToken ct = default);
    Task UpdateAsync(SubAgent subAgent, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
