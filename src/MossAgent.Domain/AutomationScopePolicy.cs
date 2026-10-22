namespace MossAgent.Domain;

/// <summary>
/// How a saved plan treats later project authorization changes.
/// </summary>
public enum AutomationScopePolicy
{
    /// <summary>
    /// Default: the plan keeps the authorization revision and root set saved with it. Any later
    /// project authorization change blocks the plan until the user re-saves it.
    /// </summary>
    FixedSnapshot = 0,

    /// <summary>
    /// Explicit opt-in saved by the user: the plan follows the project's current authorization.
    /// Never applied implicitly, because it can widen the plan's reach after the fact.
    /// </summary>
    FollowProject = 1
}
