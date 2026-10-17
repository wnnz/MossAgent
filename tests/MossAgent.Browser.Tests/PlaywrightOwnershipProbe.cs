using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightOwnershipProbe : IDisposable
{
    public PlaywrightOwnershipProbe()
    {
        Page = PlaywrightApiProxy.Create<IPage>((method, _) => method.Name switch
        {
            "get_IsClosed" => false,
            "CloseAsync" => ClosePage(),
            "add_Popup" or "remove_Popup" => null,
            _ => throw new NotSupportedException(method.Name)
        });
        Context = PlaywrightApiProxy.Create<IBrowserContext>((method, _) => method.Name switch
        {
            "get_Pages" => ReadPages(),
            "NewPageAsync" => CreatePage(),
            "CloseAsync" => CloseContext(),
            _ => throw new NotSupportedException(method.Name)
        });
        Browser = PlaywrightApiProxy.Create<IBrowser>((method, _) => method.Name switch
        {
            "get_Contexts" => ReadContexts(),
            "CloseAsync" => CloseBrowser(),
            _ => throw new NotSupportedException(method.Name)
        });
        var browserType = PlaywrightApiProxy.Create<IBrowserType>((method, _) => method.Name switch
        {
            "LaunchPersistentContextAsync" => StartLaunch(),
            "ConnectOverCDPAsync" => StartConnection(),
            _ => throw new NotSupportedException(method.Name)
        });
        Runtime = PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name switch
        {
            "get_Chromium" => browserType,
            "Dispose" => DisposeRuntime(),
            _ => throw new NotSupportedException(method.Name)
        });
        Factory = new PlaywrightBrowserSessionFactory(() => Task.FromResult(Runtime));
    }

    public IPlaywright Runtime { get; }
    public IBrowser Browser { get; }
    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public PlaywrightBrowserSessionFactory Factory { get; }
    public TaskCompletionSource<IBrowserContext> Launch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IBrowser> Connection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IPage> PageCreation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource PageStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ContextCloseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task ContextCloseTask { get; set; } = Task.CompletedTask;
    public bool HasPages { get; set; } = true;
    public int RuntimeDisposeCount { get; private set; }
    public int ContextCloseCount { get; private set; }
    public int PageCloseCount { get; private set; }
    public int BrowserCloseCount { get; private set; }
    public int ContextReadCount { get; private set; }
    public int PagesReadCount { get; private set; }
    private string ProfileDirectory { get; } = Path.Combine(Path.GetTempPath(), "MossAgent-playwright-ownership-" + Guid.NewGuid().ToString("N"));

    public BrowserLaunchOptions Options(BrowserProfileMode mode) =>
        new(BrowserEngine.Chrome, mode, ProfileDirectory,
            mode == BrowserProfileMode.ExistingCdp ? new Uri("ws://127.0.0.1:9222/devtools/browser/synthetic") : null,
            null, null, null);

    public void CompleteBrowser(BrowserProfileMode mode)
    {
        if (mode == BrowserProfileMode.ExistingCdp)
            Connection.SetResult(Browser);
        else
            Launch.SetResult(Context);
    }

    public void Dispose()
    {
        if (Directory.Exists(ProfileDirectory)) Directory.Delete(ProfileDirectory, recursive: true);
    }

    private Task<IBrowserContext> StartLaunch() => Launch.Task;
    private Task<IBrowser> StartConnection() => Connection.Task;

    private IReadOnlyList<IPage> ReadPages()
    {
        PagesReadCount++;
        return HasPages ? [Page] : [];
    }

    private IReadOnlyList<IBrowserContext> ReadContexts()
    {
        ContextReadCount++;
        return [Context];
    }

    private Task<IPage> CreatePage()
    {
        PageStarted.TrySetResult();
        return PageCreation.Task;
    }

    private Task CloseContext()
    {
        ContextCloseCount++;
        ContextCloseStarted.TrySetResult();
        return ContextCloseTask;
    }

    private Task ClosePage()
    {
        PageCloseCount++;
        return Task.CompletedTask;
    }

    private Task CloseBrowser()
    {
        BrowserCloseCount++;
        return Task.CompletedTask;
    }

    private object? DisposeRuntime()
    {
        RuntimeDisposeCount++;
        return null;
    }
}
