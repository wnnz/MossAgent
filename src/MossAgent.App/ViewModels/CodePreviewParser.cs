using System.Text.RegularExpressions;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public static class CodePreviewParser
{
    private static readonly Regex Hunk = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    public const int MaximumLines = 2000;
    public const int MaximumCharacters = 128 * 1024;

    public static IReadOnlyList<CodePreviewLine> Parse(string content, string path, bool diff)
    {
        var language = Language(path);
        var lines = ReadLines(content, out var truncated);
        var result = new List<CodePreviewLine>();
        var oldNumber = 0;
        var newNumber = 0;
        var inHunk = false;
        foreach (var line in lines)
        {
            if (!diff) { result.Add(new("", (++newNumber).ToString(), "", line, language, "context")); continue; }
            var match = Hunk.Match(line);
            if (line.StartsWith("diff --git", StringComparison.Ordinal)) inHunk = false;
            if (match.Success && int.TryParse(match.Groups[1].Value, out var hunkOld)
                && int.TryParse(match.Groups[2].Value, out var hunkNew))
            {
                oldNumber = hunkOld;
                newNumber = hunkNew;
                inHunk = true;
                result.Add(new("", "", "", line, "text", "header"));
                continue;
            }
            if (inHunk && line.Length > 0 && line[0] is '+' or '-' or ' ')
            {
                var kind = line[0] == '+' ? "added" : line[0] == '-' ? "removed" : "context";
                result.Add(new(kind == "added" ? "" : (oldNumber++).ToString(),
                    kind == "removed" ? "" : (newNumber++).ToString(), line[..1], line[1..], language, kind));
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("diff --git", StringComparison.Ordinal)
                && !line.StartsWith("index ", StringComparison.Ordinal) && !line.StartsWith("--- ", StringComparison.Ordinal)
                && !line.StartsWith("+++ ", StringComparison.Ordinal) && !line.StartsWith("warning:", StringComparison.Ordinal))
                result.Add(new("", "", "", line, "text", "header"));
        }
        if (truncated)
            result.Add(new("", "", "", InspectorText.Get("PreviewTruncated"), "text", "header"));
        return result;
    }

    internal static IReadOnlyList<string> ReadLines(string content, out bool truncated)
    {
        var limit = Math.Min(content.Length, MaximumCharacters);
        truncated = content.Length > limit;
        var lines = new List<string>();
        var start = 0;
        while (lines.Count < MaximumLines)
        {
            var newline = content.IndexOf('\n', start, limit - start);
            var end = newline < 0 ? limit : newline;
            if (newline >= 0 && end > start && content[end - 1] == '\r') end--;
            lines.Add(content[start..end]);
            if (newline < 0) return lines;
            start = newline + 1;
        }
        truncated = true;
        return lines;
    }

    public static string Language(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" => "csharp",
        ".js" or ".jsx" => "javascript",
        ".ts" or ".tsx" => "typescript",
        ".py" => "python",
        ".json" => "json",
        ".sh" or ".ps1" => "bash",
        ".sql" => "sql",
        _ => "text"
    };
}
