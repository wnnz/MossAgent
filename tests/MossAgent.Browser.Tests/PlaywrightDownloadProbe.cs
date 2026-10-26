using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightDownloadProbe : IAsyncDisposable
{
    private readonly TaskCompletionSource<string?> _failureGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _streamGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlaywrightBrowserSession? _session;
    public MemoryStream Source { get; } = new(System.Text.Encoding.UTF8.GetBytes("sdk download"));
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource StreamRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? Failure { get; init; }
    public bool HoldFailure { get; init; }
    public bool DeleteFails { get; init; }
    public bool CancelFails { get; init; }
    public string? TempPath { get; init; }
    public bool PathLookupFails { get; init; }
    public bool HoldSource { get; init; }
    public Stream? SourceOverride { get; init; }
    public int Clicks { get; private set; }
    public int Deletes { get; private set; }
    public int Cancels { get; private set; }
    public int SourceOpens { get; private set; }
    public int RuntimeDisposals { get; private set; }

    public PlaywrightBrowserSession CreateSession(IArtifactSessionJournal? journal = null, Guid? taskId = null)
    {
        var download = PlaywrightApiProxy.Create<IDownload>((method, _) => method.Name switch
        {
            "FailureAsync" => FailureAsync(),
            "PathAsync" => PathAsync(),
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
        return _session = new(runtime, context, page, closeContext: true, journal: journal, taskId: taskId);
    }

    private async Task<IDownload> StartAsync(Func<Task> click, IDownload download) { await click(); return download; }
    private Task ClickAsync() { Clicks++; return Task.CompletedTask; }
    private Task<string?> FailureAsync() { Started.TrySetResult(); return HoldFailure ? _failureGate.Task : Task.FromResult(Failure); }
    private Task<string?> PathAsync() => PathLookupFails
        ? Task.FromException<string?>(new IOException("private sdk detail"))
        : Task.FromResult(TempPath);
    private async Task<Stream> ReadAsync()
    {
        SourceOpens++;
        StreamRequested.TrySetResult();
        if (HoldSource) await _streamGate.Task;
        return SourceOverride ?? Source;
    }
    // Cancelling a download ends the SDK wait as a cancel, like the real failure signal.
    private Task CancelAsync()
    {
        Cancels++;
        _failureGate.TrySetResult("canceled");
        return CancelFails ? Task.FromException(new IOException("private sdk detail")) : Task.CompletedTask;
    }
    private Task DeleteAsync() { Deletes++; return DeleteFails ? Task.FromException(new IOException("private sdk detail")) : Task.CompletedTask; }
    private object? DisposeRuntime() { RuntimeDisposals++; _failureGate.TrySetResult(null); _streamGate.TrySetResult(); return null; }

    public async ValueTask DisposeAsync()
    {
        _failureGate.TrySetResult(null);
        _streamGate.TrySetResult();
        if (_session is not null) await _session.DisposeAsync();
        await Source.DisposeAsync();
    }
}
