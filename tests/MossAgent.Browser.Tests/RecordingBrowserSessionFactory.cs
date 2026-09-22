using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class RecordingBrowserSessionFactory :
    IBrowserSessionFactory,
    IEmbeddedBrowserSessionFactory
{
    public int CreateCount { get; private set; }
    public BrowserLaunchOptions? LastOptions { get; private set; }

    public Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        CreateCount++;
        LastOptions = options;
        return Task.FromResult<IBrowserSession>(new StubBrowserSession());
    }
}
