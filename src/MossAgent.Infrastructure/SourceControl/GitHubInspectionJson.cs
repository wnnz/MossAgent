using System.Globalization;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal static class GitHubInspectionJson
{
    public static SourceReviewException Invalid() => new("source_invalid_response");

    public static string Text(JsonElement item, string name, int limit, string token, bool allowEmpty = false)
    {
        var value = item.GetProperty(name).GetString();
        if (value is null || value.Length > limit || (!allowEmpty && value.Length == 0)
            || value.Contains(token, StringComparison.Ordinal)
            || value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            throw Invalid();
        return value;
    }

    public static string? OptionalText(JsonElement item, string name, int limit, string token, bool allowEmpty = false) =>
        !item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null
            ? null : Text(item, name, limit, token, allowEmpty);

    public static string? OptionalNestedText(JsonElement item, string parent, string name, int limit, string token)
    {
        if (!item.TryGetProperty(parent, out var nested) || nested.ValueKind == JsonValueKind.Null) return null;
        if (nested.ValueKind != JsonValueKind.Object) throw Invalid();
        return OptionalText(nested, name, limit, token);
    }

    public static DateTimeOffset? OptionalTimestamp(JsonElement item, string name, string token)
    {
        var value = OptionalText(item, name, 64, token);
        if (value is null) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
            throw Invalid();
        return parsed;
    }

    public static int Count(JsonElement item, string name)
    {
        if (!item.GetProperty(name).TryGetInt32(out var value) || value < 0) throw Invalid();
        return value;
    }

    public static long Id(JsonElement item)
    {
        if (!item.GetProperty("id").TryGetInt64(out var value) || value <= 0) throw Invalid();
        return value;
    }

    public static JsonElement[] Items(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 20) throw Invalid();
        return element.EnumerateArray().ToArray();
    }

    public static void CheckCommit(string actual, string expected)
    {
        if (!PullRequestRevision.IsCommit(actual)) throw Invalid();
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new SourceReviewException("source_revision_changed");
    }

    public static void CheckOwner(JsonElement item, string property, Uri expected, string token)
    {
        if (!item.TryGetProperty(property, out _)) return;
        var value = Text(item, property, 2048, token);
        if (!value.Equals(expected.AbsoluteUri, StringComparison.OrdinalIgnoreCase)) throw Invalid();
    }
}
