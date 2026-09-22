namespace MossAgent.Tools.Abstractions.Browser;

public interface IEmbeddedBrowserSessionFactory
{
    Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken);
}
