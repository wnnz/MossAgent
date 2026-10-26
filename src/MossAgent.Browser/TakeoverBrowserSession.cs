using MossAgent.Application.Persistence;
using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

internal sealed class TakeoverBrowserSession(
    IBrowserSession inner,
    BrowserTaskSessionState state,
    Guid taskId,
    IBrowserSessionActionJournal? actionJournal = null,
    Action? onInnerDisposed = null)
    : IBrowserSession
{
    private static readonly TimeSpan JournalResolveBudget = TimeSpan.FromSeconds(5);

    public string Id => inner.Id;
    public Uri? CurrentUri => state.IsAutomated ? inner.CurrentUri : null;
    public bool SupportsDeveloperTools => inner.SupportsDeveloperTools;

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ListTabsAsync(token), cancellationToken);

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.NewTab, null,
            (session, token) => session.NewTabAsync(uri, token), cancellationToken);

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.SwitchTabAsync(tabId, token), cancellationToken);

    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.CloseTab, null,
            (session, token) => session.CloseTabAsync(tabId, token), cancellationToken);

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.Navigate, BrowserTargetHash.ForUri(uri),
            (session, token) => session.NavigateAsync(uri, token), cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.NavigateBack, null,
            (session, token) => session.GoBackAsync(token), cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.NavigateForward, null,
            (session, token) => session.GoForwardAsync(token), cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.Reload, null,
            (session, token) => session.ReloadAsync(token), cancellationToken);

    public Task OpenDeveloperToolsAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.OpenDeveloperToolsAsync(token), cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GetPageTextAsync(token), cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GetSnapshotAsync(token), cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.Click, BrowserTargetHash.ForSelector(selector),
            (session, token) => session.ClickAsync(selector, token), cancellationToken);

    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.Type, BrowserTargetHash.ForSelector(selector),
            (session, token) => session.TypeAsync(selector, text, token), cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.SelectOption, BrowserTargetHash.ForSelector(selector),
            (session, token) => session.SelectOptionAsync(selector, value, token), cancellationToken);

    public Task WaitForAsync(
        string selector,
        BrowserElementState elementState,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.WaitForAsync(selector, elementState, timeout, token),
            cancellationToken);

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ScrollAsync(selector, deltaX, deltaY, token), cancellationToken);

    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) =>
        RunJournaledAsync(BrowserActionKind.Download, BrowserTargetHash.ForSelector(selector),
            (session, token) => session.DownloadAsync(selector, destination, token), cancellationToken);

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ScreenshotAsync(fullPage, token), cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private Task RunAsync(
        Func<IBrowserSession, CancellationToken, Task> operation,
        CancellationToken cancellationToken) =>
        RunAsync(async (session, token) =>
        {
            await operation(session, token);
            return true;
        }, cancellationToken);

    private async Task<TResult> RunAsync<TResult>(
        Func<IBrowserSession, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var linked = state.EnterAutomation(cancellationToken);
        try
        {
            return await operation(inner, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            state.ThrowIfNotAutomated();
            throw;
        }
        catch (ObjectDisposedException)
        {
            onInnerDisposed?.Invoke();
            throw new BrowserTakeoverException(
                BrowserToolText.Get("BrowserSessionDisconnected"),
                BrowserControlState.Disconnected);
        }
    }

    // Mutating operations journal a committed intent before the side effect reaches the
    // inner session. The lazy session is materialized first so its session row exists;
    // a journal failure or missing row is fail-closed — no unaudited effect is emitted.
    private Task RunJournaledAsync(
        BrowserActionKind kind,
        string? targetHash,
        Func<IBrowserSession, CancellationToken, Task> operation,
        CancellationToken cancellationToken) =>
        RunJournaledAsync(kind, targetHash, async (session, token) =>
        {
            await operation(session, token);
            return true;
        }, cancellationToken);

    private async Task<TResult> RunJournaledAsync<TResult>(
        BrowserActionKind kind,
        string? targetHash,
        Func<IBrowserSession, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (actionJournal is null)
        {
            return await RunAsync(operation, cancellationToken);
        }

        using var linked = state.EnterAutomation(cancellationToken);
        IBrowserSession target;
        Guid intentId;
        try
        {
            target = inner is LazyBrowserSession lazy
                ? await lazy.GetOrCreateAsync(linked.Token)
                : inner;
            intentId = await RecordIntentAsync(kind, targetHash, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            state.ThrowIfNotAutomated();
            throw;
        }
        catch (ObjectDisposedException)
        {
            onInnerDisposed?.Invoke();
            throw new BrowserTakeoverException(
                BrowserToolText.Get("BrowserSessionDisconnected"),
                BrowserControlState.Disconnected);
        }

        try
        {
            var result = await operation(target, linked.Token);
            await ResolveBestEffortAsync(intentId, BrowserActionIntentState.Resolved);
            return result;
        }
        catch (OperationCanceledException)
        {
            await ResolveBestEffortAsync(intentId, BrowserActionIntentState.Cancelled);
            if (linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                state.ThrowIfNotAutomated();
            }

            throw;
        }
        catch (ObjectDisposedException)
        {
            await ResolveBestEffortAsync(intentId, BrowserActionIntentState.Failed);
            onInnerDisposed?.Invoke();
            throw new BrowserTakeoverException(
                BrowserToolText.Get("BrowserSessionDisconnected"),
                BrowserControlState.Disconnected);
        }
        catch (Exception)
        {
            await ResolveBestEffortAsync(intentId, BrowserActionIntentState.Failed);
            throw;
        }
    }

    private async Task<Guid> RecordIntentAsync(
        BrowserActionKind kind,
        string? targetHash,
        CancellationToken cancellationToken)
    {
        var intentId = await actionJournal!.RecordActionAsync(taskId, kind, targetHash, cancellationToken);
        return intentId ?? throw new InvalidOperationException(
            BrowserToolText.Get("BrowserActionJournalUnavailable"));
    }

    private async Task ResolveBestEffortAsync(Guid intentId, BrowserActionIntentState outcome)
    {
        if (actionJournal is null)
        {
            return;
        }

        using var deadline = new CancellationTokenSource(JournalResolveBudget);
        try { await actionJournal.ResolveActionAsync(intentId, outcome, deadline.Token); }
        catch (Exception) { /* Best-effort: a terminal mark must never mask the action result. */ }
    }
}
