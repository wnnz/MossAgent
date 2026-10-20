namespace MossAgent.Domain;

public sealed class PullRequestPatchMap
{
    public const int MaximumCharacters = 32768;
    public const int MaximumNewlines = 1000;
    private readonly HashSet<int> _left = [];
    private readonly HashSet<int> _right = [];
    private PullRequestPatchMap() { }

    public bool Contains(string side, int line) => line > 0 && (side == "LEFT" ? _left.Contains(line) : side == "RIGHT" && _right.Contains(line));

    public bool ContainsRange(string side, int start, int end)
    {
        if (start <= 0 || end < start || (long)end - start > MaximumNewlines) return false;
        for (long line = start; line <= end; line++) if (!Contains(side, (int)line)) return false;
        return true;
    }

    public static PullRequestPatchMap? Parse(string patch, int additions, int deletions)
    {
        if (additions < 0 || deletions < 0 || patch.Length > MaximumCharacters
            || patch.Count(character => character == '\n') > MaximumNewlines) return null;
        var map = new PullRequestPatchMap();
        if (patch.Length == 0) return additions == 0 && deletions == 0 ? map : null;
        var remainingOld = 0;
        var remainingNew = 0;
        var oldLine = 0;
        var newLine = 0;
        var added = 0;
        var deleted = 0;
        var hasHunk = false;
        long oldEnd = 0;
        long newEnd = 0;
        var lines = patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (index == lines.Length - 1 && line.Length == 0) break;
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                var hunk = PullRequestPatchHunk.Parse(line);
                if (hunk is null || remainingOld != 0 || remainingNew != 0 || hunk.OldStart < oldEnd || hunk.NewStart < newEnd) return null;
                oldLine = hunk.OldStart; newLine = hunk.NewStart;
                remainingOld = hunk.OldCount; remainingNew = hunk.NewCount;
                oldEnd = (long)oldLine + remainingOld; newEnd = (long)newLine + remainingNew;
                hasHunk = true; continue;
            }
            if (!hasHunk || line.Length == 0) return null;
            if (line == "\\ No newline at end of file") continue;
            switch (line[0])
            {
                case '+': map._right.Add(newLine++); remainingNew--; added++; break;
                case '-': map._left.Add(oldLine++); remainingOld--; deleted++; break;
                case ' ': map._left.Add(oldLine++); map._right.Add(newLine++); remainingOld--; remainingNew--; break;
                default: return null;
            }
            if (remainingOld < 0 || remainingNew < 0) return null;
        }
        return hasHunk && remainingOld == 0 && remainingNew == 0 && added == additions && deleted == deletions ? map : null;
    }
}
