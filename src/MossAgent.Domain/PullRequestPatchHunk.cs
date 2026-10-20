using System.Globalization;
using System.Text.RegularExpressions;

namespace MossAgent.Domain;

internal sealed record PullRequestPatchHunk(int OldStart, int OldCount, int NewStart, int NewCount)
{
    private static readonly Regex Header = new(@"^@@ -([0-9]+)(?:,([0-9]+))? \+([0-9]+)(?:,([0-9]+))? @@(?:.*)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static PullRequestPatchHunk? Parse(string line)
    {
        var match = Header.Match(line);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var oldStart)
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var newStart)
            || !Count(match.Groups[2], out var oldCount) || !Count(match.Groups[4], out var newCount)
            || oldStart == 0 && oldCount > 0 || newStart == 0 && newCount > 0
            || (long)oldStart + oldCount > int.MaxValue || (long)newStart + newCount > int.MaxValue) return null;
        return new(oldStart, oldCount, newStart, newCount);
    }

    private static bool Count(Group group, out int count)
    {
        count = 1;
        return !group.Success || int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }
}
