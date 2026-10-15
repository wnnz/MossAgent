using Microsoft.Playwright;

namespace MossAgent.Browser.Tests;

internal sealed class TrackingPlaywrightRuntime : IPlaywright
{
    public int DisposeCount { get; private set; }
    public int BrowserAccessCount { get; private set; }
    public IBrowserType Chromium => UnexpectedBrowserAccess();
    public IBrowserType Firefox => UnexpectedBrowserAccess();
    public IBrowserType Webkit => UnexpectedBrowserAccess();
    public IBrowserType this[string browserType] => UnexpectedBrowserAccess();
    public IReadOnlyDictionary<string, BrowserNewContextOptions> Devices => new Dictionary<string, BrowserNewContextOptions>();
    public IAPIRequest APIRequest => throw new NotSupportedException();
    public ISelectors Selectors => throw new NotSupportedException();

    public void Dispose() => DisposeCount++;

    private IBrowserType UnexpectedBrowserAccess()
    {
        BrowserAccessCount++;
        throw new InvalidOperationException("A cancelled factory must not access the browser.");
    }
}
