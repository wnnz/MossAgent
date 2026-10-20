using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubPullRequestParser
{
    public static PullRequestPage ParsePage(byte[] body, SourceRepositoryScope scope, string token, int page)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20) throw Invalid();
        var items = document.RootElement.EnumerateArray().Select(item => ParseItem(item, scope, token, false)).ToArray();
        if (items.Select(item => item.Number).Distinct().Count() != items.Length) throw Invalid();
        return new PullRequestPage(items, page, items.Length == 20 && page < 1000);
    }

    public static PullRequestSummary ParseDetail(byte[] body, SourceRepositoryScope scope, string token, int number)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var result = ParseItem(document.RootElement, scope, token, true);
        if (result.Number != number) throw Invalid();
        return result;
    }

    private static PullRequestSummary ParseItem(JsonElement item, SourceRepositoryScope scope, string token, bool detail)
    {
        if (!item.GetProperty("number").TryGetInt32(out var number) || number <= 0) throw Invalid();
        var baseValue = item.GetProperty("base");
        var repository = Text(baseValue.GetProperty("repo"), "full_name", 201, token);
        if (!repository.Equals($"{scope.Owner}/{scope.Repository}", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var state = Text(item, "state", 6, token);
        if (state is not ("open" or "closed")) throw Invalid();
        var body = string.Empty;
        if (detail && item.TryGetProperty("body", out var bodyValue) && bodyValue.ValueKind != JsonValueKind.Null)
            body = Text(item, "body", 65536, token, true);
        return new PullRequestSummary(number, Text(item, "title", 1024, token),
            Text(item.GetProperty("user"), "login", 100, token), state, item.GetProperty("draft").GetBoolean(),
            body, Revision(baseValue, token), Revision(item.GetProperty("head"), token));
    }

    private static string Revision(JsonElement element, string token)
    {
        var revision = Text(element, "sha", 64, token);
        if (revision.Length is not (40 or 64) || revision.Any(character => !char.IsAsciiHexDigit(character))) throw Invalid();
        return revision;
    }

    private static string Text(JsonElement item, string name, int maximumLength, string token, bool allowEmpty = false)
    {
        var value = item.GetProperty(name).GetString();
        if (value is null || value.Length > maximumLength || (!allowEmpty && value.Length == 0)
            || value.Contains(token, StringComparison.Ordinal)
            || value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'))) throw Invalid();
        return value;
    }

    private static SourceReviewException Invalid() => new("source_invalid_response");
}
