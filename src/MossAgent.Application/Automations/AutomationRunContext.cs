using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>The snapshot-validated entities a scheduled run executes against.</summary>
public sealed record AutomationRunContext(AutomationSchedule Schedule, ProjectProfile Project,
    AiProvider Provider, ModelProfile Model);
