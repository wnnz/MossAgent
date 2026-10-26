using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class PullRequestText
{
    private static readonly ResourceManager Labels = new("MossAgent.App.Resources.PullRequestStrings", typeof(PullRequestText).Assembly);
    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing PR resource: {key}");
    public static string Title => Get(nameof(Title));
    public static string Description => Get(nameof(Description));
    public static string Refresh => Get(nameof(Refresh));
    public static string Create => Get(nameof(Create));
    public static string Repository => Get(nameof(Repository));
    public static string Load => Get(nameof(Load));
    public static string Revoke => Get(nameof(Revoke));
    public static string Previous => Get(nameof(Previous));
    public static string Next => Get(nameof(Next));
    public static string Detail => Get(nameof(Detail));
    public static string Cancel => Get(nameof(Cancel));
    public static string Close => Get(nameof(Close));
    public static string Empty => Get(nameof(Empty));
    public static string EmptyHint => Get(nameof(EmptyHint));
    public static string Account => Get(nameof(Account));
    public static string Owner => Get(nameof(Owner));
    public static string Name => Get(nameof(Name));
    public static string GrantHint => Get(nameof(GrantHint));
    public static string GrantWrite => Get(nameof(GrantWrite));
    public static string AccessReadOnly => Get(nameof(AccessReadOnly));
    public static string AccessReadWrite => Get(nameof(AccessReadWrite));
    public static string Remaining => Get(nameof(Remaining));
    public static string BaseRevision => Get(nameof(BaseRevision));
    public static string HeadRevision => Get(nameof(HeadRevision));
    public static string Inspection => Get(nameof(Inspection));
    public static string ReadSection => Get(nameof(ReadSection));
    public static string PageLimited => Get(nameof(PageLimited));
    public static string SectionEmpty => Get(nameof(SectionEmpty));
    public static string VersionHint => Get(nameof(VersionHint));
    public static string CommentHint => Get(nameof(CommentHint));
    public static string BackToList => Get(nameof(BackToList));
    public static string AnnotationHint => Get(nameof(AnnotationHint));
    public static string LocateComment => Get(nameof(LocateComment));
    public static string OpenChat => Get(nameof(OpenChat));
    public static string OpenReadOnlyChat => Get(nameof(OpenReadOnlyChat));
    public static string ReadOnlyChatHint => Get(nameof(ReadOnlyChatHint));
    public static string InvestigateChecks => Get(nameof(InvestigateChecks));
    public static string ChatHint => Get(nameof(ChatHint));
    public static string OpenLink => Get(nameof(OpenLink));
    public static string LinkUrl => Get(nameof(LinkUrl));
    public static string LinkHint => Get(nameof(LinkHint));
    public static string LinkGrantHint => Get(nameof(LinkGrantHint));
    public static string LinkStatus => Get(nameof(LinkStatus));
    public static string InboxSection => Get(nameof(InboxSection));
    public static string InboxFilter => Get(nameof(InboxFilter));
    public static string InboxLoad => Get(nameof(InboxLoad));
    public static string InboxEmpty => Get(nameof(InboxEmpty));
    public static string InboxIncomplete => Get(nameof(InboxIncomplete));
    public static string InboxDraft => Get(nameof(InboxDraft));
    public static string InboxHint => Get(nameof(InboxHint));
    public static string MarkUnviewed => Get(nameof(MarkUnviewed));
    public static string MarkViewed => Get(nameof(MarkViewed));
    public static string MarkUpdated => Get(nameof(MarkUpdated));
    public static string CommentBody => Get(nameof(CommentBody));
    public static string CommentPublish => Get(nameof(CommentPublish));
    public static string CommentConfirm => Get(nameof(CommentConfirm));
    public static string CommentConfirmHint => Get(nameof(CommentConfirmHint));
    public static string CommentPublishHint => Get(nameof(CommentPublishHint));
    public static string CommentPublishing => Get(nameof(CommentPublishing));
    public static string CommentPublished => Get(nameof(CommentPublished));
    public static string CommentLanded => Get(nameof(CommentLanded));
    public static string CommentCancelled => Get(nameof(CommentCancelled));
    public static string CommentReadOnlyHint => Get(nameof(CommentReadOnlyHint));
    public static string ReviewDecision => Get(nameof(ReviewDecision));
    public static string ReviewBody => Get(nameof(ReviewBody));
    public static string ReviewSubmit => Get(nameof(ReviewSubmit));
    public static string ReviewConfirm => Get(nameof(ReviewConfirm));
    public static string ReviewConfirmHint => Get(nameof(ReviewConfirmHint));
    public static string ReviewSubmitHint => Get(nameof(ReviewSubmitHint));
    public static string ReviewSubmitting => Get(nameof(ReviewSubmitting));
    public static string ReviewSubmitted => Get(nameof(ReviewSubmitted));
    public static string ReviewLanded => Get(nameof(ReviewLanded));
    public static string ReviewCancelled => Get(nameof(ReviewCancelled));
    public static string ReviewReadOnlyHint => Get(nameof(ReviewReadOnlyHint));
    public static string ClosePullRequest => Get(nameof(ClosePullRequest));
    public static string CloseConfirm => Get(nameof(CloseConfirm));
    public static string CloseConfirmHint => Get(nameof(CloseConfirmHint));
    public static string CloseSubmitting => Get(nameof(CloseSubmitting));
    public static string Closed => Get(nameof(Closed));
    public static string ClosedReconciled => Get(nameof(ClosedReconciled));
    public static string CloseCancelled => Get(nameof(CloseCancelled));
    public static string CloseReadOnlyHint => Get(nameof(CloseReadOnlyHint));
    public static string MergePullRequest => Get(nameof(MergePullRequest));
    public static string MergeMethod => Get(nameof(MergeMethod));
    public static string MergeConfirm => Get(nameof(MergeConfirm));
    public static string MergeConfirmHint => Get(nameof(MergeConfirmHint));
    public static string MergeSubmitting => Get(nameof(MergeSubmitting));
    public static string Merged => Get(nameof(Merged));
    public static string MergedReconciled => Get(nameof(MergedReconciled));
    public static string MergeCancelled => Get(nameof(MergeCancelled));
    public static string MergeReadOnlyHint => Get(nameof(MergeReadOnlyHint));
    public static string MergeDraftHint => Get(nameof(MergeDraftHint));
    public static string CreatePullRequest => Get(nameof(CreatePullRequest));
    public static string CreatePrBase => Get(nameof(CreatePrBase));
    public static string CreatePrHead => Get(nameof(CreatePrHead));
    public static string CreatePrTitle => Get(nameof(CreatePrTitle));
    public static string CreatePrBody => Get(nameof(CreatePrBody));
    public static string CreatePrDraft => Get(nameof(CreatePrDraft));
    public static string CreatePrHint => Get(nameof(CreatePrHint));
    public static string CreatePrConfirm => Get(nameof(CreatePrConfirm));
    public static string CreatePrConfirmHint => Get(nameof(CreatePrConfirmHint));
    public static string CreatePrSubmitting => Get(nameof(CreatePrSubmitting));
    public static string PrCreated => Get(nameof(PrCreated));
    public static string PrCreatedReconciled => Get(nameof(PrCreatedReconciled));
    public static string CreatePrLoadFailed => Get(nameof(CreatePrLoadFailed));
    public static string CreatePrCancelled => Get(nameof(CreatePrCancelled));
    public static string BranchesLoading => Get(nameof(BranchesLoading));
    public static string BranchesFailed => Get(nameof(BranchesFailed));

    public static string Failure(string code, string writeKind = "comment") => Get(code switch
    {
        "source_scope_denied" => "ScopeDenied",
        "source_invalid_pr_url" => "InvalidPrUrl",
        "source_url_account_mismatch" => "LinkAccountMismatch",
        "source_url_repository_not_granted" => "LinkRepositoryNotGranted",
        "source_revision_changed" => "RevisionChanged",
        "source_output_limit" => "OutputLimited",
        "source_auth_failed" => "AuthFailed",
        "source_forbidden" => "Forbidden",
        "source_rate_limited" => "RateLimited",
        "source_not_found" => "NotFound",
        "source_write_rejected" => WriteFailureKey(writeKind, "Rejected"),
        "source_write_unconfirmed" => WriteFailureKey(writeKind, "Unconfirmed"),
        "source_write_unknown" => WriteFailureKey(writeKind, "Unknown"),
        "source_invalid_response" or "source_redirect_denied" => "InvalidResponse",
        "tool_cancelled" or "tool_timeout" or "source_timeout" => "Cancelled",
        _ => "Failed"
    });

    private static string WriteFailureKey(string writeKind, string outcome) => writeKind switch
    {
        "review" => "ReviewWrite" + outcome,
        "close" => "Close" + outcome,
        "merge" => "Merge" + outcome,
        "create" => "CreatePr" + outcome,
        _ => "Write" + outcome
    };
}
