using Microsoft.Playwright;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightDownloadProbe : IAsyncDisposable
{
    private readonly TaskCompletionSource<string?> _failureGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlaywrightBrowserSession? _session;
    public MemoryStream Source { get; } = new(System.Text.Encoding.UTF8.GetBytes("sdk download"));
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? Failure { get; init; }
    public bool HoldFailure { get; init; }
    public bool DeleteFails { get; init; }
    public bool CancelFails { get; init; }
    public int Clicks { get; private set; }
    public int Deletes { get; private set; }
    public int Cancels { get; private set; }
    public int SourceOpens { get; private set; }
    public int RuntimeDisposals { get; private set; }

    public PlaywrightBrowserSession CreateSession()
    {
        var download = PlaywrightApiProxy.Create<IDownload>((method, _) => method.Name switch
        {
            "FailureAsync" => FailureAsync(),
            "CreateReadStreamAsync" => ReadAsync(),
            "get_SuggestedFilename" => "sdk.txt",
            "CancelAsync" => CancelAsync(),
            "DeleteAsync" => DeleteAsync(),
            _ => throw new NotSupportedException(method.Name)
        });
        var locator = PlaywrightApiProxy.Create<ILocator>((method, _) => method.Name == "ClickAsync" ? ClickAsync() : throw new NotSupportedException(method.Name));
        var page = PlaywrightApiProxy.Create<IPage>((method, arguments) => method.Name switch
        {
            "Locator" => locator,
            "RunAndWaitForDownloadAsync" => StartAsync((Func<Task>)arguments![0]!, download),
            "add_Popup" or "remove_Popup" => null,
            _ => throw new NotSupportedException(method.Name)
        });
        var context = PlaywrightApiProxy.Create<IBrowserContext>((method, _) => method.Name switch
        {
            "get_Pages" => new[] { page },
            "CloseAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        });
        var runtime = PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name == "Dispose" ? DisposeRuntime() : throw new NotSupportedException(method.Name));
        return _session = new(runtime, context, page, closeContext: true);
    }

    private async Task<IDownload> StartAsync(Func<Task> click, IDownload download) { await click(); return download; }
    private Task ClickAsync() { Clicks++; return Task.CompletedTask; }
    private Task<string?> FailureAsync() { Started.TrySetResult(); return HoldFailure ? _failureGate.Task : Task.FromResult(Failure); }
    private Task<Stream> ReadAsync() { SourceOpens++; return Task.FromResult<Stream>(Source); }
    private Task CancelAsync() { Cancels++; return CancelFails ? Task.FromException(new IOException("private sdk detail")) : Task.CompletedTask; }
    private Task DeleteAsync() { Deletes++; return DeleteFails ? Task.FromException(new IOException("private sdk detail")) : Task.CompletedTask; }
    private object? DisposeRuntime() { RuntimeDisposals++; _failureGate.TrySetResult(null); return null; }

    public async ValueTask DisposeAsync()
    {
        _failureGate.TrySetResult(null);
        if (_session is not null) await _session.DisposeAsync();
        await Source.DisposeAsync();
    }
}
