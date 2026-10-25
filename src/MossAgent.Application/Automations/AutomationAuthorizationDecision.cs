namespace MossAgent.Application.Automations;

/// <summary>Result of evaluating an automation's authorization snapshot at fire time.
/// A null <see cref="Context"/> means the run is blocked and <see cref="ErrorCode"/>
/// carries the machine-readable reason recorded on the run history row.</summary>
public sealed record AutomationAuthorizationDecision(AutomationRunContext? Context, string? ErrorCode)
{
    public bool IsAllowed => Context is not null;
}
