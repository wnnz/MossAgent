using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>Validates an automation's pinned authorization snapshot against live project,
/// provider, and model state at fire time.</summary>
public interface IAutomationAuthorizationGate
{
    Task<AutomationAuthorizationDecision> EvaluateAsync(AutomationSchedule schedule, CancellationToken cancellationToken);
}
