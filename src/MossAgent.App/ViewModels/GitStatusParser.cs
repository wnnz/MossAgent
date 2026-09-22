namespace MossAgent.App.ViewModels;

internal static class GitStatusParser
{
    public static IReadOnlyList<GitChangedFileViewModel> Parse(string content)
    {
        var nullTerminated = content.Contains('\0');
        var entries = content.Split(
            nullTerminated ? '\0' : '\n',
            StringSplitOptions.RemoveEmptyEntries);
        var files = new List<GitChangedFileViewModel>();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index].TrimEnd('\r');
            if (entry.StartsWith("##", StringComparison.Ordinal) || entry.Length < 4)
            {
                continue;
            }

            var status = entry[..2];
            if (status == "!!")
            {
                continue;
            }

            var path = entry[3..];
            if (nullTerminated && IsRenameOrCopy(status) && index + 1 < entries.Length)
            {
                index++;
            }
            else if (!nullTerminated && path.Contains(" -> ", StringComparison.Ordinal))
            {
                path = path[(path.LastIndexOf(" -> ", StringComparison.Ordinal) + 4)..];
            }

            files.Add(new GitChangedFileViewModel(
                path, status, HasStagedChange(status), HasUnstagedChange(status)));
        }

        return files.OrderBy(static file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsRenameOrCopy(string status) =>
        status.Contains('R') || status.Contains('C');

    private static bool HasStagedChange(string status) =>
        status[0] is not (' ' or '?');

    private static bool HasUnstagedChange(string status) =>
        status[1] != ' ' || status == "??";
}
