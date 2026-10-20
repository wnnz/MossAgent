using Microsoft.Playwright;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightOperationCancellationProbe(bool borrowed) : IAsyncDisposable
{
    private readonly TaskCompletionSource _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlaywrightBrowserSession? _session;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CloseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task CloseGate { get; set; } = Task.CompletedTask;
    public bool CloseFailureExpected { get; set; }
    public int RuntimeDisposals { get; private set; }
    public int ContextCloses { get; private set; }
    public int OwnedPageCloses { get; private set; }
    public int Actions { get; private set; }
    public void FailAction(Exception error) => _pending.TrySetException(error);

    public PlaywrightBrowserSession CreateSession()
    {
        var locator = PlaywrightApiProxy.Create<ILocator>((method, _) => method.Name switch
        {
            "ClickAsync" or "FillAsync" => Begin(),
            "AriaSnapshotAsync" => ReadAsync(),
            _ => throw new NotSupportedException(method.Name)
        });
        var page = PlaywrightApiProxy.Create<IPage>((method, _) => method.Name switch
        {
            "Locator" => locator,
            "GotoAsync" => NavigateAsync(),
            "get_IsClosed" => OwnedPageCloses > 0,
            "CloseAsync" => CloseAsync(true),
            "add_Popup" or "remove_Popup" => null,
            _ => throw new NotSupportedException(method.Name)
        });
        var context = PlaywrightApiProxy.Create<IBrowserContext>((method, _) => method.Name switch
        {
            "get_Pages" => new[] { page },
            "CloseAsync" => CloseAsync(false),
            _ => throw new NotSupportedException(method.Name)
        });
        var runtime = PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name switch
        {
            "Dispose" => DisposeRuntime(),
            _ => throw new NotSupportedException(method.Name)
        });
        return _session = new(runtime, context, page, closeContext: !borrowed, ownsInitialPage: borrowed);
    }

    private Task Begin()
    {
        Actions++;
        Started.TrySetResult();
        return _pending.Task;
    }

    private async Task<string> ReadAsync() { await Begin(); return "snapshot"; }
    private async Task<IResponse?> NavigateAsync() { await Begin(); return null; }

    private async Task CloseAsync(bool page)
    {
        if (page) OwnedPageCloses++; else ContextCloses++;
        CloseStarted.TrySetResult();
        await CloseGate;
    }

    private object? DisposeRuntime()
    {
        RuntimeDisposals++;
        _pending.TrySetException(new InvalidOperationException("Owned runtime disconnected."));
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_session is not null) await _session.DisposeAsync(); }
        catch (IOException) when (CloseFailureExpected) { }
        _pending.TrySetCanceled();
        await _pending.Task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
