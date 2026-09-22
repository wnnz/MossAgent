using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface ISkillRepository
{
    Task<List<Skill>> GetAllAsync(bool enabledOnly = false, CancellationToken ct = default);
    Task<Skill?> GetAsync(int id, CancellationToken ct = default);
    Task<Skill?> GetByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Skill skill, CancellationToken ct = default);
    Task UpdateAsync(Skill skill, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
