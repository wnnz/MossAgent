namespace MossAgent.Application.Automations;

/// <summary>
/// Raised when a plan's saved authorization snapshot no longer matches the project at execution
/// time. The scheduler records the run as failed with the <c>authorization-changed</c> error code
/// instead of running with wider or different roots.
/// </summary>
public sealed class AutomationScopeChangedException()
    : InvalidOperationException("The plan's saved authorization no longer matches its project.");
