namespace MossAgent.Application.Persistence;

/// <summary>
/// The mutating browser operations journaled before they reach the inner session.
/// Read-only calls (tab listing, text/snapshot reads, waits, scroll, screenshots and
/// developer tools) are never journaled; storage values are lowercase machine names.
/// </summary>
public enum BrowserActionKind
{
    Navigate,
    NavigateBack,
    NavigateForward,
    Reload,
    NewTab,
    CloseTab,
    Click,
    Type,
    SelectOption,
    Download
}
