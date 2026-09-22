using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface ISettingsRepository
{
    Task<List<AppSetting>> GetAllAsync(CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
}
