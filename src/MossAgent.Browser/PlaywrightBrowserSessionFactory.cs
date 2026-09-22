using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class PlaywrightBrowserSessionFactory : IBrowserSessionFactory
{
    public async Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        var playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
        try
        {
            return options.ProfileMode == BrowserProfileMode.ExistingCdp
                ? await ConnectAsync(playwright, options, cancellationToken)
                : await LaunchAsync(playwright, options, cancellationToken);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    private static async Task<IBrowserSession> LaunchAsync(
        IPlaywright playwright,
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ProfileDirectory))
        {
            throw new InvalidOperationException("启动浏览器需要资料目录。");
        }

        Directory.CreateDirectory(options.ProfileDirectory);
        var launch = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            Channel = options.Engine == BrowserEngine.Edge ? "msedge" : "chrome",
            Proxy = CreateProxy(options)
        };
        var context = await playwright.Chromium
            .LaunchPersistentContextAsync(options.ProfileDirectory, launch)
            .WaitAsync(cancellationToken);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        return new PlaywrightBrowserSession(playwright, context, page, closeContext: true);
    }

    private static async Task<IBrowserSession> ConnectAsync(
        IPlaywright playwright,
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = options.CdpEndpoint
            ?? throw new InvalidOperationException("连接已有浏览器需要 CDP 地址。");
        var browser = await playwright.Chromium
            .ConnectOverCDPAsync(endpoint.ToString())
            .WaitAsync(cancellationToken);
        var context = browser.Contexts.FirstOrDefault()
            ?? throw new InvalidOperationException("CDP 浏览器没有可用上下文。");
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        return new PlaywrightBrowserSession(playwright, context, page, closeContext: false);
    }

    private static Proxy? CreateProxy(BrowserLaunchOptions options)
    {
        return options.ProxyUri is null
            ? null
            : new Proxy
            {
                Server = options.ProxyUri.ToString(),
                Username = options.ProxyUsername,
                Password = options.ProxyPassword
            };
    }
}

