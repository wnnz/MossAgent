using MossAgent.App.Resources;
using MossAgent.Application.Persistence;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Projects one journaled <see cref="BrowserActionIntentRecord"/> into the audit pane.
/// A still-committed intent is the pending side effect: it is rendered as running and
/// the session row surfaces the unresolved warning instead of replaying anything.
/// </summary>
public sealed class AuditBrowserActionViewModel
{
    public AuditBrowserActionViewModel(BrowserActionIntentRecord record) => Record = record;

    public BrowserActionIntentRecord Record { get; }
    public string KindLabel => InspectorText.Get(Record.Kind switch
    {
        BrowserActionKind.Navigate => "AuditBrowserActionNavigate",
        BrowserActionKind.NavigateBack => "AuditBrowserActionNavigateBack",
        BrowserActionKind.NavigateForward => "AuditBrowserActionNavigateForward",
        BrowserActionKind.Reload => "AuditBrowserActionReload",
        BrowserActionKind.NewTab => "AuditBrowserActionNewTab",
        BrowserActionKind.CloseTab => "AuditBrowserActionCloseTab",
        BrowserActionKind.Click => "AuditBrowserActionClick",
        BrowserActionKind.Type => "AuditBrowserActionType",
        BrowserActionKind.SelectOption => "AuditBrowserActionSelectOption",
        _ => "AuditBrowserActionDownload"
    });
    public string OutcomeKind => Record.State switch
    {
        BrowserActionIntentState.Committed => "running",
        BrowserActionIntentState.Resolved => "success",
        BrowserActionIntentState.Cancelled => "cancelled",
        BrowserActionIntentState.Interrupted => "interrupted",
        _ => "failed"
    };
    public string OutcomeLabel => InspectorText.Get(OutcomeKind switch
    {
        "success" => "Succeeded",
        "running" => "Pending",
        "interrupted" => "AuditInterrupted",
        "cancelled" => "AuditCancelled",
        _ => "Failed"
    });
    public bool IsPending => Record.State == BrowserActionIntentState.Committed;
    public string TargetLabel => Record.TargetHash is null
        ? string.Empty
        : InspectorText.Format("AuditBrowserTarget", Record.TargetHash);
    public string Metadata => InspectorText.Format("AuditMetadata", Record.CreatedAt.ToLocalTime(),
        Record.State == BrowserActionIntentState.Committed
            ? InspectorText.Get("Pending")
            : Record.UpdatedAt >= Record.CreatedAt
                ? InspectorText.Format("AuditDuration",
                    Math.Ceiling((Record.UpdatedAt - Record.CreatedAt).TotalMilliseconds))
                : InspectorText.Get("AuditNotFinished"));
}
