using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceRepositoryGrants
{
    Task<IReadOnlyList<SourceRepositoryScope>> GetAllAsync(CancellationToken cancellationToken);
    Task<SourceRepositoryScope?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(SourceRepositoryScope scope, CancellationToken cancellationToken);
    Task RevokeAsync(Guid id, CancellationToken cancellationToken);
}
