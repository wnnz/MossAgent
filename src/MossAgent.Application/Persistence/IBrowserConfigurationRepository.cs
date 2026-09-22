using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IBrowserConfigurationRepository
{
    Task<BrowserConfiguration> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(BrowserConfiguration configuration, CancellationToken cancellationToken);
}

