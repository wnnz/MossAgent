using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>
/// Plan-level authorization and parameter snapshot rules.
/// A plan saved today binds the project authorization revision, the root set and the model
/// parameters observed at save time. Claim and execution re-check them, so later project or model
/// edits cannot widen an existing plan without an explicit re-save.
/// </summary>
public static class AutomationScopeRules
{
    /// <summary>
    /// Effective policy for a plan. Plans saved before snapshots existed were never bound to a
    /// revision, so they are treated as following the project and become fixed once re-saved.
    /// </summary>
    /// <param name="schedule">Plan being claimed or executed.</param>
    public static AutomationScopePolicy PolicyOf(AutomationSchedule schedule) =>
        schedule.Scope?.Policy ?? AutomationScopePolicy.FollowProject;

    /// <summary>
    /// Builds the snapshot saved with a plan from the project and model currently selected.
    /// </summary>
    /// <param name="project">Project whose authorization values are captured.</param>
    /// <param name="model">Model whose parameters are captured; null keeps protocol defaults.</param>
    /// <param name="policy">Policy the user selected for this plan.</param>
    public static AutomationScopeSnapshot CreateScope(ProjectProfile project, AutomationScopePolicy policy) =>
        new(project.AuthorizationRevision, project.AuthorizedDirectories.ToArray(), policy);

    /// <summary>
    /// Builds the model parameter snapshot saved with a plan.
    /// </summary>
    /// <param name="model">Selected model profile.</param>
    public static AutomationModelSnapshot CreateModel(ModelProfile model) =>
        new(model.ReasoningEffort, model.Temperature, model.TopP, model.AdvancedParametersJson);

    /// <summary>
    /// Checks whether the saved snapshot still matches the project's current authorization.
    /// </summary>
    /// <param name="project">Project row read at claim or execution time.</param>
    /// <param name="scope">Snapshot saved with the plan.</param>
    public static bool Matches(ProjectProfile project, AutomationScopeSnapshot scope) =>
        project.AuthorizationRevision == scope.ProjectRevision
        && new HashSet<string>(project.AuthorizedDirectories, StringComparer.Ordinal)
            .SetEquals(scope.AuthorizedRoots);

    /// <summary>
    /// Applies the saved model parameters onto the current model profile, so a scheduled run
    /// reproduces the plan instead of silently using later settings edits.
    /// </summary>
    /// <param name="schedule">Plan holding the snapshot; plans without one keep the current values.</param>
    /// <param name="model">Currently selected model row.</param>
    public static ModelProfile ResolveModel(AutomationSchedule schedule, ModelProfile model) =>
        schedule.Model is not { } saved ? model : model with
        {
            ReasoningEffort = saved.ReasoningEffort,
            Temperature = saved.Temperature,
            TopP = saved.TopP,
            AdvancedParametersJson = saved.AdvancedParametersJson
        };

    /// <summary>
    /// Validates the optional snapshot fields of a plan. Legacy plans without snapshots stay valid.
    /// </summary>
    /// <param name="schedule">Plan to validate.</param>
    /// <exception cref="ArgumentException">Thrown when a present snapshot is out of its accepted range.</exception>
    public static void Validate(AutomationSchedule schedule)
    {
        if (schedule.Scope is { } scope)
        {
            if (scope.ProjectRevision < 1 || !Enum.IsDefined(scope.Policy) || scope.AuthorizedRoots is null
                || scope.AuthorizedRoots.Count > 64
                || scope.AuthorizedRoots.Any(root => string.IsNullOrWhiteSpace(root) || root.Length > 32767
                    || root.Contains('\0'))
                || scope.AuthorizedRoots.Distinct(StringComparer.Ordinal).Count() != scope.AuthorizedRoots.Count)
                throw new ArgumentException("Invalid automation authorization snapshot.");
        }
        if (schedule.Model is { } model)
        {
            if (model.ReasoningEffort is null || model.ReasoningEffort.Length > 256
                || model.AdvancedParametersJson is null || model.AdvancedParametersJson.Length > 32768
                || model.Temperature is < 0 or > 2 || model.TopP is < 0 or > 1)
                throw new ArgumentException("Invalid automation model snapshot.");
        }
    }
}
