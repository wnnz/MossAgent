namespace MossAgent.Domain;

/// <summary>
/// Compares an automation's pinned authorization snapshot with the live project/provider/model
/// state. A non-null result is a machine-readable reason code that must block the run.
/// </summary>
public static class AutomationSnapshotReview
{
    public const string MissingSnapshot = "snapshot-missing";
    public const string Mismatch = "snapshot-mismatch";
    public const string ProjectMissing = "project-unavailable";
    public const string RevisionChanged = "authorization-revision-changed";
    public const string DirectoriesReduced = "authorization-directories-reduced";
    public const string ProviderMissing = "provider-unavailable";
    public const string ModelMissing = "model-unavailable";

    /// <summary>Returns null when the snapshot may run; otherwise the reason code that blocks it.
    /// The provider and model arguments must already be filtered to enabled entities.</summary>
    public static string? Evaluate(AutomationSchedule schedule, ProjectProfile? project,
        AiProvider? provider, ModelProfile? model)
    {
        var snapshot = schedule.AuthorizationSnapshot;
        if (snapshot is null) return MissingSnapshot;
        if (snapshot.ProjectId != schedule.ProjectId || snapshot.ProviderId != schedule.ProviderId
            || snapshot.ModelId != schedule.ModelId) return Mismatch;
        if (project is null) return ProjectMissing;
        // FollowAuthorizationChanges is an explicit plan-level opt-in: the run then uses the
        // project's current roots, so authorization revision and directory drift no longer
        // block it. Provider and model remain pinned to the snapshot in both modes.
        if (!schedule.FollowAuthorizationChanges
            && project.AuthorizationRevision != snapshot.ProjectAuthorizationRevision) return RevisionChanged;
        if (provider is null) return ProviderMissing;
        if (!schedule.FollowAuthorizationChanges)
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var authorized = new HashSet<string>(project.AuthorizedDirectories, comparer);
            if (snapshot.AuthorizedDirectories.Any(directory => !authorized.Contains(directory)))
                return DirectoriesReduced;
        }
        return model is null ? ModelMissing : null;
    }
}
