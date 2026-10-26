namespace MossAgent.Application.Persistence;

/// <summary>
/// Journal projection of one journaled browser action intent. <see cref="Committed"/>
/// means the side effect was dispatched but its outcome was not yet confirmed;
/// <see cref="Interrupted"/> is written only by startup reconciliation and is never replayed.
/// </summary>
public enum BrowserActionIntentState
{
    Committed,
    Resolved,
    Failed,
    Cancelled,
    Interrupted
}
