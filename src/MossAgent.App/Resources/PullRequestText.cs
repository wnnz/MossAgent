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

    public static string Failure(string code) => Get(code switch
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
        "source_invalid_response" or "source_redirect_denied" => "InvalidResponse",
        "tool_cancelled" or "tool_timeout" or "source_timeout" => "Cancelled",
        _ => "Failed"
    });
}
