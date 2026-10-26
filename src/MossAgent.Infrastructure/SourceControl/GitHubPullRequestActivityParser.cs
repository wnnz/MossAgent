using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestActivityParser
{
    public static PullRequestInspectionPage<PullRequestActivity> Parse(byte[] body, SourceAccount account,
        SourceRepositoryScope scope, PullRequestRevision revision, int page)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var items = GitHubInspectionJson.Items(document.RootElement)
            .Select(item => ParseItem(item, account, scope)).ToArray();
        if (items.Sum(item => item.Detail?.Length ?? 0) > 65536
            || items.Select(item => item.Id).Distinct().Count() != items.Length)
            throw GitHubInspectionJson.Invalid();
        return new(revision, items, page, items.Length == 20 && page < 1000, IsLimited: items.Length == 20 && page == 1000);
    }

    private static PullRequestActivity ParseItem(JsonElement item, SourceAccount account, SourceRepositoryScope scope)
    {
        var token = account.AccessToken;
        var id = GitHubInspectionJson.Id(item);
        var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/issues/events/{id}");
        GitHubInspectionJson.CheckOwner(item, "url", expected, token);
        var actor = item.GetProperty("actor").ValueKind == JsonValueKind.Null ? string.Empty
            : GitHubInspectionJson.Text(item.GetProperty("actor"), "login", 100, token);
        var commit = GitHubInspectionJson.OptionalText(item, "commit_id", 64, token);
        if (commit is not null && !PullRequestRevision.IsCommit(commit)) throw GitHubInspectionJson.Invalid();
        return new(id, GitHubInspectionJson.Text(item, "event", 64, token), actor, Detail(item, token),
            commit, GitHubInspectionJson.OptionalTimestamp(item, "created_at", token));
    }

    private static string? Detail(JsonElement item, string token)
    {
        if (GitHubInspectionJson.OptionalNestedText(item, "label", "name", 100, token) is { } label) return label;
        if (GitHubInspectionJson.OptionalNestedText(item, "assignee", "login", 100, token) is { } assignee) return assignee;
        var from = GitHubInspectionJson.OptionalNestedText(item, "rename", "from", 512, token);
        var to = GitHubInspectionJson.OptionalNestedText(item, "rename", "to", 512, token);
        return from is null && to is null ? null : $"{from} → {to}";
    }
}
