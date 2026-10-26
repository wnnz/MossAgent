using System.Text.Json;

namespace MossAgent.Testing;

internal static class PullRequestTestData
{
    public static string Item(int number = 7, string repository = "fixture/repo", string title = "Review this change", string body = "Untrusted PR description") =>
        JsonSerializer.Serialize(new
        {
            number, title, body, state = "open", draft = false,
            user = new { login = "fixture-user" },
            @base = new { sha = new string('a', 40), repo = new { full_name = repository } },
            head = new { sha = new string('b', 40) }
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

    public static string Checks(bool runs = true, string? head = null) => runs
        ? JsonSerializer.Serialize(new { total_count = 1, check_runs = new[] { new { id = 21, name = "Build", status = "completed",
            conclusion = "success", head_sha = head ?? new string('b', 40), details_url = "https://attacker.invalid/log" } } })
        : JsonSerializer.Serialize(new { total_count = 1, sha = head ?? new string('b', 40), statuses = new[]
            { new { id = 22, context = "Legacy CI", state = "pending", target_url = "https://attacker.invalid/log" } } });
}
