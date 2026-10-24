namespace MossAgent.App.Tests;

internal static class LocalizableText
{
    public static string? Unwrap(string? value) => value is { } text
        && text.StartsWith('{') && text.EndsWith("'}") && text.Contains(":L '")
        ? text[(text.IndexOf(":L '", StringComparison.Ordinal) + 4)..^2]
        : value;
}
