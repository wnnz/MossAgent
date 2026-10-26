using MossAgent.Application.Persistence;
using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

/// <summary>
/// Session-lifecycle journal marks for <see cref="TaskBrowserSessionManager"/>. Creation
/// registration is fail-closed (an unjournaled session could never journal its mutating
/// intents, so the just-created session is disposed instead); detached/disconnected marks
/// are best-effort and never mask the release or disconnect itself.
/// </summary>
internal sealed class BrowserSessionJournalGate(IBrowserSessionJournal? journal)
{
    public async Task RecordSessionOrDisposeAsync(
        Guid taskId,
        BrowserLaunchOptions options,
        IBrowserSession session,
        CancellationToken cancellationToken)
    {
        if (journal is null)
        {
            return;
        }

        try
        {
            if (await journal.RecordSessionAsync(
                    taskId, options.Engine, options.ProfileMode, cancellationToken))
            {
                return;
            }
        }
        catch (Exception)
        {
            await DisposeQuietlyAsync(session);
            throw;
        }

        await DisposeQuietlyAsync(session);
        throw new InvalidOperationException(BrowserToolText.Get("BrowserSessionJournalUnavailable"));
    }

    public async Task RecordDetachedBestEffortAsync(Guid taskId)
    {
        if (journal is null)
        {
            return;
        }

        try { await journal.RecordDetachedAsync(taskId, CancellationToken.None); }
        catch (Exception) { /* Best-effort journal mark; release must still complete. */ }
    }

    public async Task RecordDisconnectedBestEffortAsync(Guid taskId)
    {
        if (journal is null)
        {
            return;
        }

        try { await journal.RecordDisconnectedAsync(taskId, CancellationToken.None); }
        catch (Exception) { /* Best-effort journal mark; never masks the disconnect. */ }
    }

    private static async Task DisposeQuietlyAsync(IBrowserSession session)
    {
        try { await session.DisposeAsync(); }
        catch (Exception) { /* Best-effort cleanup on the journal-failure path. */ }
    }
}
