using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class RecordingBrowserSessionFactory :
    IBrowserSessionFactory,
    IEmbeddedBrowserSessionFactory
{
    public int CreateCount { get; private set; }
    public BrowserLaunchOptions? LastOptions { get; private set; }
    public StubBrowserSession? LastSession { get; private set; }
    public IBrowserSession? LastCreated { get; private set; }
    public Func<IBrowserSession>? SessionSource { get; set; }
    public bool FailNextCreate { get; set; }

    public Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        if (FailNextCreate)
        {
            FailNextCreate = false;
            return Task.FromException<IBrowserSession>(
                new InvalidOperationException("launch failed"));
        }

        CreateCount++;
        LastOptions = options;
        LastCreated = SessionSource?.Invoke() ?? new StubBrowserSession();
        LastSession = LastCreated as StubBrowserSession;
        return Task.FromResult(LastCreated);
    }
}
