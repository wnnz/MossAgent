using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface ISubagentConfigurationRepository
{
    Task<IReadOnlyList<SubagentProfile>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAsync(SubagentProfile profile, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
