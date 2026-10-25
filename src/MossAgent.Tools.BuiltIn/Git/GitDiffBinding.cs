using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Processes;

namespace MossAgent.Tools.BuiltIn.Git;

public static class GitDiffBinding
{
    public const string Marker = "--- mossagent ---";
    public const string DiffHashPrefix = "diff-sha256:";
    public const string FileHashPrefix = "file-sha256:";
    private const string SegmentPrefix = "diff --git ";

    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string ReadExpectedDiff(JsonElement arguments)
    {
        if (arguments.TryGetProperty("expectedDiff", out var element)
            && element.ValueKind == JsonValueKind.String
            && element.GetString() is { Length: 64 } value
            && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            return value;
        }

        throw new ArgumentException();
    }

    public static ToolResult Bind(ToolResult result, BoundedTextBuffer captured)
    {
        if (!result.IsSuccess || captured.IsTruncated) return result;
        var text = captured.ToString();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var content = result.Content ?? string.Empty;
        return result with { Content = content + (content.EndsWith('\n') ? "" : "\n") + Block(text) };
    }

    private static string Block(string captured)
    {
        var builder = new StringBuilder(256 + captured.Length / 8);
        builder.Append(Marker).Append('\n').Append(DiffHashPrefix).Append(Hash(captured)).Append('\n');
        var start = SegmentStart(captured, 0);
        while (start >= 0)
        {
            var next = SegmentStart(captured, start + SegmentPrefix.Length);
            var end = next >= 0 ? next : captured.Length;
            builder.Append(FileHashPrefix).Append(Hash(captured[start..end])).Append(' ')
                .Append(BPathLabel(captured, start)).Append('\n');
            start = next;
        }

        return builder.ToString();
    }

    private static int SegmentStart(string text, int fromIndex)
    {
        if (fromIndex == 0 && text.StartsWith(SegmentPrefix, StringComparison.Ordinal)) return 0;
        var index = text.IndexOf("\n" + SegmentPrefix, fromIndex, StringComparison.Ordinal);
        return index < 0 ? -1 : index + 1;
    }

    private static string BPathLabel(string text, int start)
    {
        var lineEnd = text.IndexOf('\n', start);
        if (lineEnd < 0) lineEnd = text.Length;
        var header = text[start..lineEnd];
        var marker = header.IndexOf(" b/", StringComparison.Ordinal);
        return marker < 0 ? header[SegmentPrefix.Length..] : header[(marker + 3)..];
    }
}
