using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IAgentConfigurationRepository
{
    Task<IReadOnlyList<AgentProfile>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentProfile profile, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
