namespace MossAgent.Domain;

public sealed record SourceReviewChatBinding(Guid TaskId, SourceRepositoryScope Scope, PullRequestRevision Revision, string Context,
    SourceReviewChatMode Mode = SourceReviewChatMode.Snapshot)
{
    public const int MaximumContextLength = 24576;

    public void Validate()
    {
        Revision.Validate();
        if (!Enum.IsDefined(Mode) || TaskId == Guid.Empty || Scope.GrantId == Guid.Empty || Scope.AccountId == Guid.Empty || Scope.AccountRevision <= 0
            || SourceRepositoryScope.ValidateSegment(Scope.Owner, true) != Scope.Owner
            || SourceRepositoryScope.ValidateSegment(Scope.Repository) != Scope.Repository
            || string.IsNullOrWhiteSpace(Context) || Context.Length > MaximumContextLength || Context.Contains('\0'))
            throw new SourceReviewException("invalid_arguments");
    }
}
