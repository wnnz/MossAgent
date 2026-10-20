using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceReviewSessions
{
    Task<Guid> GetOrCreateAsync(SourceRepositoryScope scope, CancellationToken cancellationToken);
}
