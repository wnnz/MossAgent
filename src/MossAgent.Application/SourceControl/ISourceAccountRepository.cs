using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceAccountRepository
{
    Task<IReadOnlyList<SourceAccount>> GetAllAsync(CancellationToken cancellationToken);
    Task<SourceAccount?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<SourceAccount> SaveAsync(SourceAccount account, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken);
}
