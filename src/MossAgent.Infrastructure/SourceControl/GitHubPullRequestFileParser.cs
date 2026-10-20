using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestFileParser
{
    public static PullRequestInspectionPage<PullRequestFile> Parse(byte[] body, PullRequestRevision revision, int page, string token)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var budget = 65536;
        var files = GitHubInspectionJson.Items(document.RootElement).Select(item => ParseFile(item, token, ref budget)).ToArray();
        if (files.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count() != files.Length)
            throw GitHubInspectionJson.Invalid();
        return new(revision, files, page, files.Length == 20 && page < 150,
            IsLimited: files.Length == 20 && page == 150);
    }

    private static PullRequestFile ParseFile(JsonElement item, string token, ref int budget)
    {
        var path = GitHubInspectionJson.Text(item, "filename", 4096, token);
        var previous = GitHubInspectionJson.OptionalText(item, "previous_filename", 4096, token);
        var status = GitHubInspectionJson.Text(item, "status", 32, token);
        if (status is not ("added" or "removed" or "modified" or "renamed" or "copied" or "changed" or "unchanged"))
            throw GitHubInspectionJson.Invalid();
        var additions = GitHubInspectionJson.Count(item, "additions");
        var deletions = GitHubInspectionJson.Count(item, "deletions");
        var patch = GitHubInspectionJson.OptionalText(item, "patch", 2 * 1024 * 1024, token, true);
        var state = PullRequestPatchState.Missing;
        if (patch is not null)
        {
            state = patch.Length > PullRequestPatchMap.MaximumCharacters || patch.Length > budget
                || patch.Count(character => character == '\n') > PullRequestPatchMap.MaximumNewlines ? PullRequestPatchState.TooLarge
                : PullRequestPatchMap.Parse(patch, additions, deletions) is not null ? PullRequestPatchState.Available : PullRequestPatchState.Incomplete;
            if (state == PullRequestPatchState.TooLarge) patch = string.Empty;
            else budget -= patch.Length;
        }
        return new(path, previous, status, additions, deletions, patch ?? string.Empty, state);
    }
}
