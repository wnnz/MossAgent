using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightCdpSessionProbe
{
    private readonly List<PlaywrightCdpPageProbe> _pages = [];

    public PlaywrightCdpSessionProbe()
    {
        Existing = CreatePage("user-page");
        Context = PlaywrightApiProxy.Create<IBrowserContext>((method, _) => method.Name switch
        {
            "get_Pages" => _pages.Where(page => !page.IsClosed).Select(page => page.Page).ToArray(),
            "NewPageAsync" => NewPage(),
            "CloseAsync" => CloseContext(),
            _ => throw new NotSupportedException(method.Name)
        });
        var browser = PlaywrightApiProxy.Create<IBrowser>((method, _) => method.Name switch
        {
            "get_Contexts" => new[] { Context },
            "CloseAsync" => CloseBrowser(),
            _ => throw new NotSupportedException(method.Name)
        });
        var browserType = PlaywrightApiProxy.Create<IBrowserType>((method, _) => method.Name switch
        {
            "ConnectOverCDPAsync" => Task.FromResult(browser),
            _ => throw new NotSupportedException(method.Name)
        });
        Factory = new PlaywrightBrowserSessionFactory(() => Task.FromResult(
            PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name switch
            {
                "get_Chromium" => browserType,
                "Dispose" => DisposeRuntime(),
                _ => throw new NotSupportedException(method.Name)
            })));
    }

    public IBrowserContext Context { get; }
    public PlaywrightCdpPageProbe Existing { get; }
    public PlaywrightBrowserSessionFactory Factory { get; }
    public IReadOnlyList<PlaywrightCdpPageProbe> Pages => _pages;
    public Func<Task<IPage>>? CreatePageOverride { get; set; }
    public int NewPageCount { get; private set; }
    public int ContextCloseCount { get; private set; }
    public int BrowserCloseCount { get; private set; }
    public int RuntimeDisposeCount { get; private set; }

    public Task<IBrowserSession> ConnectAsync(CancellationToken token) => Factory.CreateAsync(
        new BrowserLaunchOptions(BrowserEngine.Chrome, BrowserProfileMode.ExistingCdp,
            null, new Uri("ws://127.0.0.1:9222/devtools/browser/test-owned"), null, null, null, Guid.NewGuid()), token);

    public PlaywrightCdpPageProbe CreatePage(string name)
    {
        var page = new PlaywrightCdpPageProbe(name);
        _pages.Add(page);
        return page;
    }

    private Task<IPage> NewPage()
    {
        NewPageCount++;
        return CreatePageOverride?.Invoke() ?? Task.FromResult(CreatePage($"task-page-{NewPageCount}").Page);
    }

    private Task CloseContext() { ContextCloseCount++; return Task.CompletedTask; }
    private Task CloseBrowser() { BrowserCloseCount++; return Task.CompletedTask; }
    private object? DisposeRuntime() { RuntimeDisposeCount++; return null; }
}
