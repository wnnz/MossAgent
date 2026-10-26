using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestReviewParser
{
    public static PullRequestInspectionPage<PullRequestReview> Parse(byte[] body, SourceAccount account,
        SourceRepositoryScope scope, PullRequestRevision revision, int page)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var items = GitHubInspectionJson.Items(document.RootElement)
            .Select(item => ParseItem(item, account, scope, revision)).ToArray();
        if (items.Sum(item => item.Body.Length) > 65536 || items.Select(item => item.Id).Distinct().Count() != items.Length)
            throw GitHubInspectionJson.Invalid();
        return new(revision, items, page, items.Length == 20 && page < 1000, IsLimited: items.Length == 20 && page == 1000);
    }

    private static PullRequestReview ParseItem(JsonElement item, SourceAccount account,
        SourceRepositoryScope scope, PullRequestRevision revision)
    {
        var token = account.AccessToken;
        var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}");
        GitHubInspectionJson.CheckOwner(item, "pull_request_url", expected, token);
        var author = item.GetProperty("user").ValueKind == JsonValueKind.Null ? string.Empty
            : GitHubInspectionJson.Text(item.GetProperty("user"), "login", 100, token);
        var state = GitHubInspectionJson.Text(item, "state", 32, token);
        if (state is not ("APPROVED" or "CHANGES_REQUESTED" or "COMMENTED" or "DISMISSED" or "PENDING"))
            throw GitHubInspectionJson.Invalid();
        var commit = GitHubInspectionJson.OptionalText(item, "commit_id", 64, token);
        if (commit is not null && !PullRequestRevision.IsCommit(commit)) throw GitHubInspectionJson.Invalid();
        return new(GitHubInspectionJson.Id(item), author, state,
            GitHubInspectionJson.OptionalText(item, "body", 16384, token, true) ?? string.Empty,
            commit, GitHubInspectionJson.OptionalTimestamp(item, "submitted_at", token));
    }
}
