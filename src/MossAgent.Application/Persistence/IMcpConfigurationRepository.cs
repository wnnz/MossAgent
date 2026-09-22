using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IMcpConfigurationRepository
{
    Task<IReadOnlyList<McpServerProfile>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAsync(McpServerProfile profile, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

