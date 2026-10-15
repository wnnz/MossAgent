using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationRunViewModel(AutomationRun run)
{
    public AutomationRun Run { get; } = run;
    public string StatusLabel => AutomationText.Get(Run.Status.ToString());
    public string TriggerLabel => AutomationText.Get(Run.IsManual ? "Manual" : "Scheduled");
    public DateTimeOffset StartedAt => Run.StartedAt.ToLocalTime();
    public bool IsCompleted => Run.Status == AutomationRunStatus.Completed;
    public bool IsFailure => Run.Status is AutomationRunStatus.Failed or AutomationRunStatus.TimedOut;
    public bool NeedsAttention => Run.Status is AutomationRunStatus.Cancelled or AutomationRunStatus.Interrupted;
    public string Detail => Run.Status switch
    {
        AutomationRunStatus.Failed => AutomationText.Get(Run.ErrorCode == "execution-unavailable" ? "ExecutionUnavailable" : "RunFailedHint"),
        AutomationRunStatus.TimedOut => AutomationText.Get("RunTimedOutHint"),
        AutomationRunStatus.Interrupted => AutomationText.Get("RunInterruptedHint"),
        AutomationRunStatus.Cancelled => AutomationText.Get("RunCancelledHint"),
        _ => string.Empty
    };
    public bool HasDetail => Detail.Length > 0;
    public string DurationLabel => Run.FinishedAt is { } finished && finished >= Run.StartedAt
        ? AutomationText.Format("RunDuration", Math.Ceiling((finished - Run.StartedAt).TotalSeconds))
        : AutomationText.Get("RunNotFinished");
}
