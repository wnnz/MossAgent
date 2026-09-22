using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Browser.Tests;

internal sealed class StubBrowserConfigurationRepository(
    BrowserConfiguration configuration) : IBrowserConfigurationRepository
{
    public Task<BrowserConfiguration> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(configuration);

    public Task SaveAsync(
        BrowserConfiguration updatedConfiguration,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
