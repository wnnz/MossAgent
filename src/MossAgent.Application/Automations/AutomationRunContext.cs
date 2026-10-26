using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>The snapshot-validated entities a scheduled run executes against.
/// <see cref="AuthorizedRoots"/> is the save-time pinned root set; null means the run
/// follows the project's current authorized directories (explicit plan opt-in).</summary>
public sealed record AutomationRunContext(AutomationSchedule Schedule, ProjectProfile Project,
    AiProvider Provider, ModelProfile Model, IReadOnlyList<string>? AuthorizedRoots = null);
