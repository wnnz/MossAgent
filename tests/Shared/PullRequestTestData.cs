using System.Text.Json;

namespace MossAgent.Testing;

internal static class PullRequestTestData
{
    public static string Item(int number = 7, string repository = "fixture/repo", string title = "Review this change",
        string body = "Untrusted PR description", string state = "open", bool draft = false,
        string? head = null, string? baseSha = null) =>
        JsonSerializer.Serialize(new
        {
            number, title, body, state, draft,
            url = $"https://api.github.com/repos/{repository}/pulls/{number}",
            user = new { login = "fixture-user" },
            @base = new { sha = baseSha ?? new string('a', 40), repo = new { full_name = repository } },
            head = new { sha = head ?? new string('b', 40) }
        });

    // Detail shape after/for a merge: merged + merge_commit_sha ride on the same pulls/{n}
    // payload the summary parser reads; the base sha may differ from the captured revision
    // because a landed merge advances it — the merge reconcile must not compare base.
    public static string Merged(int number = 7, string repository = "fixture/repo", bool merged = true,
        string? head = null, string? baseSha = null, string? mergeCommit = null) =>
        JsonSerializer.Serialize(new
        {
            number, title = "Review this change", body = "Untrusted PR description",
            state = merged ? "closed" : "open", draft = false, merged,
            merge_commit_sha = mergeCommit,
            url = $"https://api.github.com/repos/{repository}/pulls/{number}",
            user = new { login = "fixture-user" },
            @base = new { sha = baseSha ?? new string('a', 40), repo = new { full_name = repository } },
            head = new { sha = head ?? new string('b', 40) }
        });

    public static string File(string? patch = "@@ -1 +1 @@\n-old\n+new", string path = "src/sample.cs", int additions = 1, int deletions = 1) =>
        JsonSerializer.Serialize(new { filename = path, status = "modified", additions, deletions, patch,
            raw_url = "https://attacker.invalid/content" });

    public static string Comment(bool inline = false, string body = "Untrusted comment", string? commit = null,
        int? line = 1, string? side = "RIGHT", int? start = null, string? startSide = null, string path = "src/sample.cs", long id = 11) =>
        JsonSerializer.Serialize(new { id, user = new { login = "reviewer" }, body,
            path, line, side, start_line = start, start_side = startSide, commit_id = commit ?? new string('b', 40),
            issue_url = "https://api.github.com/repos/fixture/repo/issues/7",
            pull_request_url = "https://api.github.com/repos/fixture/repo/pulls/7", kind = inline ? "inline" : "conversation" });

    public static string Review(long id = 31, object? user = null, string state = "APPROVED", string? body = "Untrusted review",
        string? commit = null, string? submitted = "2024-01-02T03:04:05Z", string repository = "fixture/repo", int number = 7) =>
        JsonSerializer.Serialize(new { id, user = user ?? new { login = "reviewer" }, body, state,
            commit_id = commit ?? new string('b', 40), submitted_at = submitted,
            pull_request_url = $"https://api.github.com/repos/{repository}/pulls/{number}" });

    public static string Event(long id = 41, string name = "labeled", object? actor = null, string? label = "bug",
        string? commit = null, string? created = "2024-01-02T03:04:05Z", string repository = "fixture/repo") =>
        JsonSerializer.Serialize(new { id, url = $"https://api.github.com/repos/{repository}/issues/events/{id}",
            actor = actor ?? new { login = "actor" }, @event = name, commit_id = commit, created_at = created,
            label = label is null ? null : (object?)new { name = label } });

    public static string InboxItem(long id = 51, int number = 7, string repository = "fixture/repo",
        string title = "Inbox pull request", string? state = "open", object? user = null,
        object? draft = null, string? updated = "2024-01-02T03:04:05Z", bool pullRequest = true,
        string? repositoryUrl = null)
    {
        var item = new Dictionary<string, object?>
        {
            ["id"] = id, ["number"] = number, ["title"] = title, ["state"] = state,
            ["draft"] = draft ?? false, ["user"] = user ?? new { login = "octocat" },
            ["repository_url"] = repositoryUrl ?? $"https://api.github.com/repos/{repository}",
            ["updated_at"] = updated
        };
        if (pullRequest) item["pull_request"] = new { url = $"https://api.github.com/repos/{repository}/pulls/{number}" };
        return JsonSerializer.Serialize(item);
    }

    public static string InboxPage(int total = 1, bool incomplete = false, params string[] items) =>
        "{\"total_count\":" + total + ",\"incomplete_results\":" + (incomplete ? "true" : "false")
            + ",\"items\":[" + string.Join(',', items) + "]}";

    public static string Checks(bool runs = true, string? head = null) => runs
        ? JsonSerializer.Serialize(new { total_count = 1, check_runs = new[] { new { id = 21, name = "Build", status = "completed",
            conclusion = "success", head_sha = head ?? new string('b', 40), details_url = "https://attacker.invalid/log" } } })
        : JsonSerializer.Serialize(new { total_count = 1, sha = head ?? new string('b', 40), statuses = new[]
            { new { id = 22, context = "Legacy CI", state = "pending", target_url = "https://attacker.invalid/log" } } });
}
