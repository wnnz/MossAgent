using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestCommentParser
{
    public static PullRequestInspectionPage<PullRequestComment> Parse(byte[] body, SourceAccount account,
        SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var items = GitHubInspectionJson.Items(document.RootElement)
            .Select(item => ParseItem(item, account, scope, revision, kind)).ToArray();
        if (items.Sum(item => item.Body.Length) > 65536 || items.Select(item => item.Id).Distinct().Count() != items.Length)
            throw GitHubInspectionJson.Invalid();
        return new(revision, items, page, items.Length == 20 && page < 1000, IsLimited: items.Length == 20 && page == 1000);
    }

    private static PullRequestComment ParseItem(JsonElement item, SourceAccount account,
        SourceRepositoryScope scope, PullRequestRevision revision, string kind)
    {
        var token = account.AccessToken;
        var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/{(kind == "inline" ? "pulls" : "issues")}/{revision.Number}");
        GitHubInspectionJson.CheckOwner(item, kind == "inline" ? "pull_request_url" : "issue_url", expected, token);
        var author = item.GetProperty("user").ValueKind == JsonValueKind.Null ? string.Empty
            : GitHubInspectionJson.Text(item.GetProperty("user"), "login", 100, token);
        var path = kind == "inline" ? GitHubInspectionJson.Text(item, "path", 4096, token) : null;
        var commit = kind == "inline" ? GitHubInspectionJson.Text(item, "commit_id", 64, token) : null;
        if (commit is not null && !PullRequestRevision.IsCommit(commit)) throw GitHubInspectionJson.Invalid();
        int? line = null;
        if (kind == "inline" && item.TryGetProperty("line", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            line = GitHubInspectionJson.Count(item, "line");
            if (line == 0) throw GitHubInspectionJson.Invalid();
        }
        var side = kind == "inline" ? GitHubInspectionJson.OptionalText(item, "side", 5, token) : null;
        if (side is not (null or "LEFT" or "RIGHT")) throw GitHubInspectionJson.Invalid();
        int? start = null;
        if (kind == "inline" && item.TryGetProperty("start_line", out var startValue) && startValue.ValueKind != JsonValueKind.Null)
        {
            start = GitHubInspectionJson.Count(item, "start_line");
            if (start == 0) throw GitHubInspectionJson.Invalid();
        }
        var startSide = kind == "inline" ? GitHubInspectionJson.OptionalText(item, "start_side", 5, token) : null;
        if (startSide is not (null or "LEFT" or "RIGHT")) throw GitHubInspectionJson.Invalid();
        return new(GitHubInspectionJson.Id(item), kind, author,
            GitHubInspectionJson.Text(item, "body", 16384, token, true), path, line, side, commit, start, startSide);
    }
}
