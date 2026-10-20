using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestCheckParser
{
    public static PullRequestInspectionPage<PullRequestCheck> Parse(byte[] body, PullRequestRevision revision,
        string kind, int page, string token)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var root = document.RootElement;
        var runs = kind == "runs";
        if (!runs) GitHubInspectionJson.CheckCommit(GitHubInspectionJson.Text(root, "sha", 64, token), revision.HeadRevision);
        var items = GitHubInspectionJson.Items(root.GetProperty(runs ? "check_runs" : "statuses"))
            .Select(item => ParseItem(item, revision, runs, token)).ToArray();
        var total = GitHubInspectionJson.Count(root, "total_count");
        if (items.Select(item => item.Id).Distinct().Count() != items.Length || total < items.Length)
            throw GitHubInspectionJson.Invalid();
        var more = (long)page * 20 < total;
        return new(revision, items, page, more && page < 1000, total, more && page == 1000);
    }

    private static PullRequestCheck ParseItem(JsonElement item, PullRequestRevision revision, bool runs, string token)
    {
        if (runs) GitHubInspectionJson.CheckCommit(GitHubInspectionJson.Text(item, "head_sha", 64, token), revision.HeadRevision);
        return new(GitHubInspectionJson.Id(item), GitHubInspectionJson.Text(item, runs ? "name" : "context", 1024, token),
            GitHubInspectionJson.Text(item, runs ? "status" : "state", 64, token),
            runs ? GitHubInspectionJson.OptionalText(item, "conclusion", 100, token) : null, runs ? "runs" : "statuses");
    }
}
