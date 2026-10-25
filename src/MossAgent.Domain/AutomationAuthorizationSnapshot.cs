namespace MossAgent.Domain;

/// <summary>
/// Immutable authorization snapshot captured when a scheduled automation is created or edited.
/// Runs execute against this pinned authorization instead of live-resolved configuration.
/// </summary>
public sealed class AutomationAuthorizationSnapshot : IEquatable<AutomationAuthorizationSnapshot>
{
    public AutomationAuthorizationSnapshot(long generation, Guid projectId, long projectAuthorizationRevision,
        IReadOnlyList<string> authorizedDirectories, Guid providerId, string providerName, Guid modelId,
        string modelName, ApprovalPolicy approvalPolicy, string prompt, DateTimeOffset createdAt)
    {
        if (generation < 1) throw new ArgumentOutOfRangeException(nameof(generation));
        if (projectAuthorizationRevision < 1) throw new ArgumentOutOfRangeException(nameof(projectAuthorizationRevision));
        if (authorizedDirectories.Count == 0) throw new ArgumentException("A snapshot requires authorized directories.", nameof(authorizedDirectories));
        Generation = generation;
        ProjectId = projectId;
        ProjectAuthorizationRevision = projectAuthorizationRevision;
        AuthorizedDirectories = authorizedDirectories.ToArray();
        ProviderId = providerId;
        ProviderName = providerName;
        ModelId = modelId;
        ModelName = modelName;
        ApprovalPolicy = approvalPolicy;
        Prompt = prompt;
        CreatedAt = createdAt;
    }

    public long Generation { get; }
    public Guid ProjectId { get; }
    public long ProjectAuthorizationRevision { get; }
    public IReadOnlyList<string> AuthorizedDirectories { get; }
    public Guid ProviderId { get; }
    public string ProviderName { get; }
    public Guid ModelId { get; }
    public string ModelName { get; }
    public ApprovalPolicy ApprovalPolicy { get; }
    public string Prompt { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Captures the authorization that is current at save time. The saved directory
    /// list is the project's resolved authorized directory set, which already includes the
    /// primary directory.</summary>
    public static AutomationAuthorizationSnapshot Capture(ProjectProfile project, AiProvider provider,
        ModelProfile model, string prompt, ApprovalPolicy approvalPolicy, long generation, DateTimeOffset createdAt) =>
        new(generation, project.Id, project.AuthorizationRevision, project.AuthorizedDirectories.ToArray(),
            provider.Id, provider.Name, model.Id,
            string.IsNullOrWhiteSpace(model.DisplayName) ? model.ModelId : model.DisplayName,
            approvalPolicy, prompt, createdAt);

    public bool Equals(AutomationAuthorizationSnapshot? other) => other is not null
        && Generation == other.Generation
        && ProjectId == other.ProjectId
        && ProjectAuthorizationRevision == other.ProjectAuthorizationRevision
        && AuthorizedDirectories.SequenceEqual(other.AuthorizedDirectories)
        && ProviderId == other.ProviderId
        && ProviderName == other.ProviderName
        && ModelId == other.ModelId
        && ModelName == other.ModelName
        && ApprovalPolicy == other.ApprovalPolicy
        && Prompt == other.Prompt
        && CreatedAt == other.CreatedAt;

    public override bool Equals(object? obj) => Equals(obj as AutomationAuthorizationSnapshot);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Generation);
        hash.Add(ProjectId);
        hash.Add(ProjectAuthorizationRevision);
        foreach (var directory in AuthorizedDirectories) hash.Add(directory);
        hash.Add(ProviderId);
        hash.Add(ProviderName);
        hash.Add(ModelId);
        hash.Add(ModelName);
        hash.Add(ApprovalPolicy);
        hash.Add(Prompt);
        hash.Add(CreatedAt);
        return hash.ToHashCode();
    }

    public static bool operator ==(AutomationAuthorizationSnapshot? left, AutomationAuthorizationSnapshot? right) =>
        left is null ? right is null : left.Equals(right);
    public static bool operator !=(AutomationAuthorizationSnapshot? left, AutomationAuthorizationSnapshot? right) => !(left == right);
}
