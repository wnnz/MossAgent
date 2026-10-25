using System.Text;
using System.Text.RegularExpressions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public static class GitHunkPatchBuilder
{
    private static readonly Regex HunkHeader = new(
        @"^@@ -\d+(?:,\d+)? \+\d+(?:,\d+)? @@", RegexOptions.CultureInvariant);

    public static GitHunkPatchResult Build(string diffOutput, IReadOnlyCollection<int> ordinals)
    {
        ArgumentNullException.ThrowIfNull(diffOutput);
        ArgumentNullException.ThrowIfNull(ordinals);
        if (ordinals.Count == 0) throw new ArgumentException("At least one hunk ordinal is required.", nameof(ordinals));

        var header = new List<string>(16);
        var hunks = new List<List<string>>(8);
        List<string>? current = null;
        var modeChange = false;
        var lines = diffOutput.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length == 0 && index == lines.Length - 1) break;
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (header.Count != 0 || hunks.Count != 0) throw Refused("GitHunksMalformed");
                header.Add(line);
                continue;
            }
            if (HunkHeader.IsMatch(line))
            {
                current = [line];
                hunks.Add(current);
                continue;
            }
            if (current is not null)
            {
                if (line.Length == 0 || line[0] is ' ' or '+' or '-' or '\\') { current.Add(line); continue; }
                throw Refused("GitHunksMalformed");
            }
            if (line.StartsWith("diff --cc", StringComparison.Ordinal)
                || line.StartsWith("diff --combined", StringComparison.Ordinal)
                || line.StartsWith("* Unmerged path", StringComparison.Ordinal)
                || line.StartsWith("@@@", StringComparison.Ordinal)) throw Refused("GitHunksConflict");
            if (line.StartsWith("Binary files", StringComparison.Ordinal)
                || line.StartsWith("GIT binary patch", StringComparison.Ordinal)) throw Refused("GitHunksBinary");
            if (line.StartsWith("similarity index", StringComparison.Ordinal)
                || line.StartsWith("dissimilarity index", StringComparison.Ordinal)
                || line.StartsWith("rename from", StringComparison.Ordinal)
                || line.StartsWith("rename to", StringComparison.Ordinal)
                || line.StartsWith("copy from", StringComparison.Ordinal)
                || line.StartsWith("copy to", StringComparison.Ordinal)) throw Refused("GitHunksRenameOrCopy");
            if (line.StartsWith("new file mode", StringComparison.Ordinal)) throw Refused("GitHunksNewFile");
            if (line.StartsWith("deleted file mode", StringComparison.Ordinal)) throw Refused("GitHunksDeletedFile");
            if (line.StartsWith("old mode", StringComparison.Ordinal)
                || line.StartsWith("new mode", StringComparison.Ordinal))
            {
                modeChange = true;
                continue;
            }
            if (line.StartsWith("index ", StringComparison.Ordinal)
                || line.StartsWith("--- ", StringComparison.Ordinal)
                || line.StartsWith("+++ ", StringComparison.Ordinal)) { header.Add(line); continue; }
            throw Refused("GitHunksMalformed");
        }

        if (header.Count == 0) throw Refused("GitHunksMalformed");
        if (hunks.Count == 0)
        {
            if (modeChange) throw Refused("GitHunksModeOnly");
            throw new GitHunksException("git_hunks_none", ToolExecutionText.Get("GitHunksNone"));
        }
        if (ordinals.Max() >= hunks.Count)
        {
            throw new GitHunksException("git_hunks_out_of_range",
                ToolExecutionText.Format("GitHunksOutOfRange", hunks.Count - 1, hunks.Count));
        }

        var selected = ordinals.Distinct().Order().ToArray();
        var patch = new StringBuilder(diffOutput.Length);
        foreach (var line in header) patch.Append(line).Append('\n');
        foreach (var ordinal in selected)
        {
            foreach (var line in hunks[ordinal]) patch.Append(line).Append('\n');
        }
        return new GitHunkPatchResult(patch.ToString(), selected.Length, hunks.Count);
    }

    private static GitHunksException Refused(string resourceKey) =>
        new("git_hunks_unsupported", ToolExecutionText.Get(resourceKey));
}
