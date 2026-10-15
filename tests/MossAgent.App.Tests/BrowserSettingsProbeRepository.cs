using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class BrowserSettingsProbeRepository : IBrowserConfigurationRepository
{
    public BrowserConfiguration Configuration { get; set; } =
        new(DefaultBrowserKind.Embedded, BrowserProfilePreference.Managed, null, null, null);
    public Func<CancellationToken, Task<BrowserConfiguration>>? Loader { get; set; }
    public Func<CancellationToken, Task>? BeforeWrite { get; set; }
    public List<BrowserConfiguration> Writes { get; } = [];
    public int Reads { get; private set; }

    public Task<BrowserConfiguration> GetAsync(CancellationToken token)
    {
        Reads++;
        return Loader?.Invoke(token) ?? Task.FromResult(Configuration);
    }

    public async Task SaveAsync(BrowserConfiguration configuration, CancellationToken token)
    {
        Writes.Add(configuration);
        if (BeforeWrite is { } beforeWrite) await beforeWrite(token);
        token.ThrowIfCancellationRequested();
        Configuration = configuration;
    }
}
