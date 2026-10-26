using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Projects one journaled <see cref="BrowserSessionRecord"/> plus its action intents into
/// the audit pane. Identity stays non-sensitive: only engine and profile mode labels are
/// shown, never profile paths, CDP endpoints or page URLs. An interrupted session or a
/// still-committed intent surfaces the unresolved warning; nothing is replayed.
/// </summary>
public sealed class AuditBrowserSessionViewModel
{
    public AuditBrowserSessionViewModel(
        BrowserSessionRecord record,
        IReadOnlyList<BrowserActionIntentRecord> intents)
    {
        Record = record;
        Actions = intents.Select(intent => new AuditBrowserActionViewModel(intent)).ToArray();
    }

    public BrowserSessionRecord Record { get; }
    public IReadOnlyList<AuditBrowserActionViewModel> Actions { get; }
    public bool HasActions => Actions.Count > 0;
    public string Title => InspectorText.Format("AuditBrowserSessionSummary", EngineLabel, ProfileLabel);
    public string EngineLabel => InspectorText.Get(Record.Engine switch
    {
        BrowserEngine.Chrome => "AuditBrowserEngineChrome",
        BrowserEngine.Edge => "AuditBrowserEngineEdge",
        _ => "AuditBrowserEngineEmbedded"
    });
    public string ProfileLabel => InspectorText.Get(Record.ProfileMode switch
    {
        BrowserProfileMode.DailyProfile => "AuditBrowserProfileDaily",
        BrowserProfileMode.ExistingCdp => "AuditBrowserProfileExistingCdp",
        _ => "AuditBrowserProfileManaged"
    });
    public string OutcomeKind => Record.State switch
    {
        BrowserSessionState.Attached => "running",
        BrowserSessionState.Detached => "success",
        BrowserSessionState.Interrupted => "interrupted",
        _ => "failed"
    };
    public string OutcomeLabel => InspectorText.Get(Record.State switch
    {
        BrowserSessionState.Attached => "AuditBrowserSessionAttached",
        BrowserSessionState.Detached => "AuditBrowserSessionDetached",
        BrowserSessionState.Interrupted => "AuditInterrupted",
        _ => "AuditBrowserSessionDisconnected"
    });
    public bool HasPendingAction => Actions.Any(action => action.IsPending);
    public string Detail => Record.State switch
    {
        BrowserSessionState.Interrupted => InspectorText.Get("AuditBrowserSessionInterruptedHint"),
        BrowserSessionState.Disconnected => InspectorText.Get("AuditBrowserSessionDisconnectedHint"),
        _ => HasPendingAction ? InspectorText.Get("AuditBrowserPendingIntentHint") : string.Empty
    };
    public string DurationLabel => Record.State == BrowserSessionState.Attached
        ? InspectorText.Get("Pending")
        : Record.UpdatedAt >= Record.CreatedAt
            ? InspectorText.Format("AuditDuration", Math.Ceiling((Record.UpdatedAt - Record.CreatedAt).TotalMilliseconds))
            : InspectorText.Get("AuditNotFinished");
    public string Metadata => InspectorText.Format("AuditMetadata", Record.CreatedAt.ToLocalTime(), DurationLabel);
}
