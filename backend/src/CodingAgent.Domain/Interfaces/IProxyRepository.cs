using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface IProxyRepository
{
    Task<List<ProxyServer>> GetAllAsync(CancellationToken ct = default);
    Task<ProxyServer?> GetAsync(int id, CancellationToken ct = default);
    Task AddAsync(ProxyServer proxy, CancellationToken ct = default);
    Task UpdateAsync(ProxyServer proxy, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
