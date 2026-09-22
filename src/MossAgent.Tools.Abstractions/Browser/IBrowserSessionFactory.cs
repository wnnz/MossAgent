namespace MossAgent.Tools.Abstractions.Browser;

public interface IBrowserSessionFactory
{
    Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken);
}

