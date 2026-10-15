using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class RejectingBrowserSessionFactory :
    IBrowserSessionFactory, IEmbeddedBrowserSessionFactory
{
    public BrowserLaunchOptions? LastOptions { get; private set; }
    public int CreateCount { get; private set; }

    public Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        LastOptions = options;
        CreateCount++;
        return Task.FromException<IBrowserSession>(new InvalidOperationException("Test browser factory reached."));
    }
}
