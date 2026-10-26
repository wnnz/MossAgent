using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestInboxParser
{
    public static PullRequestInboxPage Parse(byte[] body, SourceAccount account, int page)
    {
        var token = account.AccessToken;
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
        var total = TotalCount(root);
        var incomplete = Incomplete(root);
        var items = GitHubInspectionJson.Items(root.GetProperty("items"))
            .Select(item => ParseItem(item, account, token)).ToArray();
        if (items.Select(item => item.Id).Distinct().Count() != items.Length) throw GitHubInspectionJson.Invalid();
        var next = items.Length == 20 && page < 1000 && (long)page * 20 < total;
        return new PullRequestInboxPage(items, page, next, total, incomplete);
    }

    private static PullRequestInboxItem ParseItem(JsonElement item, SourceAccount account, string token)
    {
        if (!item.TryGetProperty("pull_request", out var link) || link.ValueKind != JsonValueKind.Object)
            throw GitHubInspectionJson.Invalid();
        var (owner, repository) = Repository(item, account, token);
        if (!item.GetProperty("number").TryGetInt32(out var number) || number <= 0) throw GitHubInspectionJson.Invalid();
        var state = GitHubInspectionJson.Text(item, "state", 6, token);
        if (state is not ("open" or "closed")) throw GitHubInspectionJson.Invalid();
        var author = item.GetProperty("user").ValueKind == JsonValueKind.Null ? string.Empty
            : GitHubInspectionJson.Text(item.GetProperty("user"), "login", 100, token);
        return new PullRequestInboxItem(GitHubInspectionJson.Id(item), number,
            GitHubInspectionJson.Text(item, "title", 512, token), author, owner, repository, state,
            Draft(item), GitHubInspectionJson.OptionalTimestamp(item, "updated_at", token));
    }

    private static (string Owner, string Repository) Repository(JsonElement item, SourceAccount account, string token)
    {
        var url = GitHubInspectionJson.Text(item, "repository_url", 2048, token);
        var prefix = new Uri(account.ApiEndpoint, "repos/").AbsoluteUri;
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw GitHubInspectionJson.Invalid();
        var segments = url[prefix.Length..].Split('/');
        try
        {
            if (segments.Length != 2) throw GitHubInspectionJson.Invalid();
            return (SourceRepositoryScope.ValidateSegment(segments[0], owner: true),
                SourceRepositoryScope.ValidateSegment(segments[1]));
        }
        catch (ArgumentException) { throw GitHubInspectionJson.Invalid(); }
    }

    private static long TotalCount(JsonElement root)
    {
        if (!root.GetProperty("total_count").TryGetInt64(out var total) || total < 0)
            throw GitHubInspectionJson.Invalid();
        return total;
    }

    private static bool Incomplete(JsonElement root)
    {
        if (!root.TryGetProperty("incomplete_results", out var value) || value.ValueKind == JsonValueKind.Null)
            return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw GitHubInspectionJson.Invalid();
        return value.GetBoolean();
    }

    private static bool Draft(JsonElement item)
    {
        if (!item.TryGetProperty("draft", out var value) || value.ValueKind == JsonValueKind.Null) return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw GitHubInspectionJson.Invalid();
        return value.GetBoolean();
    }
}
