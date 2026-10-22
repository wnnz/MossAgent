namespace MossAgent.Domain;

/// <summary>
/// Project authorization values saved with a plan, plus the policy that decides whether later
/// project authorization changes block the plan or are followed.
/// </summary>
/// <param name="ProjectRevision">Project authorization revision observed when the plan was saved.</param>
/// <param name="AuthorizedRoots">
/// Authorized directories observed when the plan was saved; stored verbatim so the claim transaction
/// can compare it with the project row without re-deriving order.
/// </param>
/// <param name="Policy">Whether the saved values stay fixed or follow the project.</param>
public sealed record AutomationScopeSnapshot(
    long ProjectRevision,
    IReadOnlyList<string> AuthorizedRoots,
    AutomationScopePolicy Policy)
{
    /// <summary>
    /// Compares the root set by value. Record equality would compare the list reference, so a plan
    /// reloaded from storage would never equal the draft that produced it.
    /// </summary>
    /// <param name="other">Snapshot to compare with.</param>
    public bool Equals(AutomationScopeSnapshot? other) =>
        other is not null && ProjectRevision == other.ProjectRevision && Policy == other.Policy
        && AuthorizedRoots.SequenceEqual(other.AuthorizedRoots, StringComparer.Ordinal);

    public override int GetHashCode() =>
        HashCode.Combine(ProjectRevision, Policy, AuthorizedRoots.Count);
}
