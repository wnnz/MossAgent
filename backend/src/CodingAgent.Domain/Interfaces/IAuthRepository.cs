using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface IAuthRepository
{
    Task<AuthRecord?> GetAsync(CancellationToken ct = default);
    Task AddAsync(AuthRecord record, CancellationToken ct = default);
    Task UpdateAsync(AuthRecord record, CancellationToken ct = default);
}
