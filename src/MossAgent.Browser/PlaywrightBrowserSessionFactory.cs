using Microsoft.Playwright;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class PlaywrightBrowserSessionFactory : IBrowserSessionFactory
{
    private readonly Func<Task<IPlaywright>> _createRuntime;

    public PlaywrightBrowserSessionFactory() : this(Playwright.CreateAsync) { }

    internal PlaywrightBrowserSessionFactory(Func<Task<IPlaywright>> createRuntime)
    {
        ArgumentNullException.ThrowIfNull(createRuntime);
        _createRuntime = createRuntime;
    }

    public async Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri? cdpEndpoint = null;
        if (options.ProfileMode == BrowserProfileMode.ExistingCdp
            && !BrowserCdpEndpointPolicy.IsAllowed(options.CdpEndpoint))
        {
            throw new InvalidOperationException(
                "连接已有浏览器仅允许不含凭据的本机 HTTP(S) 或 WebSocket(S) CDP 地址。");
        }

        if (options.ProfileMode == BrowserProfileMode.ExistingCdp)
        {
            cdpEndpoint = await CdpEndpointResolver.ResolveAsync(
                options.CdpEndpoint!, cancellationToken);
        }

        var playwright = await CreateRuntimeAsync(cancellationToken).ConfigureAwait(false);
        IBrowserSession? session = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            session = cdpEndpoint is not null
                ? await ConnectAsync(playwright, cdpEndpoint, cancellationToken)
                : await LaunchAsync(playwright, options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return session;
        }
        catch
        {
            if (session is null)
                playwright.Dispose();
            else
                await session.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<IPlaywright> CreateRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _createRuntime().ConfigureAwait(false);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static async Task<IBrowserSession> LaunchAsync(
        IPlaywright playwright,
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(options.ProfileDirectory))
        {
            throw new InvalidOperationException("启动浏览器需要资料目录。");
        }

        Directory.CreateDirectory(options.ProfileDirectory);
        var launch = CreateLaunchOptions(options);
        IBrowserContext? context = null;
        try
        {
            context = await playwright.Chromium
                .LaunchPersistentContextAsync(options.ProfileDirectory, launch).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new PlaywrightBrowserSession(playwright, context, page, closeContext: true);
        }
        catch
        {
            if (context is not null)
                await context.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static BrowserTypeLaunchPersistentContextOptions CreateLaunchOptions(BrowserLaunchOptions options) => new()
    {
        Headless = false,
        ChromiumSandbox = true,
        Channel = options.Engine == BrowserEngine.Edge ? "msedge" : "chrome",
        Proxy = CreateProxy(options)
    };

    private static async Task<IBrowserSession> ConnectAsync(
        IPlaywright playwright,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var browser = await playwright.Chromium
            .ConnectOverCDPAsync(endpoint.ToString()).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        IPage? createdPage = null;
        try
        {
            var context = browser.Contexts.FirstOrDefault()
                ?? throw new InvalidOperationException("CDP 浏览器没有可用上下文。");
            var page = context.Pages.FirstOrDefault()
                ?? (createdPage = await context.NewPageAsync().ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            return new PlaywrightBrowserSession(playwright, context, page, closeContext: false);
        }
        catch
        {
            if (createdPage is not null)
                await createdPage.CloseAsync().ConfigureAwait(false);
            throw;
        }
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
