using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceAccountIdentityClient
{
    Task<SourceAccountIdentity> GetIdentityAsync(SourceAccount account, CancellationToken cancellationToken);
}
