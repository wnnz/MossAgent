using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MossAgent.Tools.BuiltIn.Files;

internal static class FileChangePatchHeaders
{
    public static string Rewrite(string patch, string path, string root)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var before = JsonSerializer.Serialize("a/" + relative, options);
        var after = JsonSerializer.Serialize("b/" + relative, options);
        var builder = new StringBuilder();
        var inHunk = false;
        var offset = 0;
        while (offset < patch.Length)
        {
            var end = patch.IndexOf('\n', offset);
            if (end < 0) end = patch.Length;
            var line = patch[offset..end];
            inHunk |= line.StartsWith("@@", StringComparison.Ordinal);
            if (!inHunk)
            {
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) line = $"diff --git {before} {after}";
                else if (line.StartsWith("--- ", StringComparison.Ordinal)) line = "--- " + before;
                else if (line.StartsWith("+++ ", StringComparison.Ordinal)) line = "+++ " + after;
            }
            builder.Append(line).Append('\n');
            offset = end + 1;
        }
        return builder.ToString();
    }
}
