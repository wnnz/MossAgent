using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationRunViewModel(AutomationRun run, long? currentGeneration = null)
{
    public AutomationRun Run { get; } = run;
    public string StatusLabel => AutomationText.Get(Run.Status.ToString());
    public string TriggerLabel => AutomationText.Get(Run.IsManual ? "Manual" : "Scheduled");
    public DateTimeOffset StartedAt => Run.StartedAt.ToLocalTime();
    public bool IsCompleted => Run.Status == AutomationRunStatus.Completed;
    public bool IsFailure => Run.Status is AutomationRunStatus.Failed or AutomationRunStatus.TimedOut;
    public bool IsStaleGeneration => Run.SnapshotGeneration is { } generation && generation != currentGeneration;
    public bool NeedsAttention => Run.Status is AutomationRunStatus.Cancelled or AutomationRunStatus.Interrupted
        or AutomationRunStatus.Blocked || IsStaleGeneration;
    public string GenerationLabel => Run.SnapshotGeneration is { } generation
        ? AutomationText.Format("RunGeneration", generation)
        : AutomationText.Get("RunGenerationUnknown");
    public string Detail
    {
        get
        {
            var detail = Run.Status switch
            {
                AutomationRunStatus.Failed => AutomationText.Get(Run.ErrorCode == "execution-unavailable" ? "ExecutionUnavailable" : "RunFailedHint"),
                AutomationRunStatus.TimedOut => AutomationText.Get("RunTimedOutHint"),
                AutomationRunStatus.Interrupted => AutomationText.Get("RunInterruptedHint"),
                AutomationRunStatus.Cancelled => AutomationText.Get("RunCancelledHint"),
                AutomationRunStatus.Blocked => AutomationText.Get(BlockedReasonKey),
                _ => string.Empty
            };
            // Blocked runs never executed, so they cannot have run under an older snapshot.
            if (!IsStaleGeneration || Run.Status == AutomationRunStatus.Blocked) return detail;
            var stale = AutomationText.Get("RunStaleGenerationHint");
            return detail.Length == 0 ? stale : $"{detail} {stale}";
        }
    }
    private string BlockedReasonKey => Run.ErrorCode switch
    {
        "authorization-revision-changed" => "RunBlockedReauthorization",
        "authorization-directories-reduced" => "RunBlockedDirectoriesReduced",
        "provider-unavailable" => "RunBlockedProviderMissing",
        "model-unavailable" => "RunBlockedModelMissing",
        "project-unavailable" => "RunBlockedProjectMissing",
        "snapshot-mismatch" => "RunBlockedSnapshotMismatch",
        "snapshot-missing" => "RunBlockedSnapshotMissing",
        _ => "RunBlockedGeneric"
    };
    public bool HasDetail => Detail.Length > 0;
    public string DurationLabel => Run.FinishedAt is { } finished && finished >= Run.StartedAt
        ? AutomationText.Format("RunDuration", Math.Ceiling((finished - Run.StartedAt).TotalSeconds))
        : AutomationText.Get("RunNotFinished");
}
