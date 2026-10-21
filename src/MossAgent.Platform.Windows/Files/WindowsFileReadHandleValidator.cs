using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsFileReadHandleValidator : IFileReadHandleValidator
{
    public void Validate(FileStream stream, IReadOnlyList<string> authorizedRoots)
    {
        try
        {
            // NT volume paths distinguish mounted volumes as well as DOS drive aliases.
            var actual = WindowsFileHandlePaths.FinalPath(stream.SafeFileHandle, 2);
            foreach (var root in authorizedRoots)
                if (IsWithinRoot(actual, root)) return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or ObjectDisposedException or NotSupportedException)
        {
            // Native error messages and physical paths must not escape to tools/audits.
        }
        throw new UnauthorizedAccessException(WindowsFileText.ReadHandleDenied);
    }

    private static bool IsWithinRoot(string actual, string root)
    {
        if (!Path.IsPathFullyQualified(root)) return false;
        var requested = NormalizeDosPath(Path.GetFullPath(root));
        using var directory = WindowsFileHandlePaths.OpenDirectory(root);
        if (directory.IsInvalid) return false;
        var resolved = NormalizeDosPath(WindowsFileHandlePaths.FinalPath(directory, 0));
        // A substituted root must not silently grant its new physical target. Aliases
        // which cannot be matched to their long DOS name are deliberately refused.
        if (!requested.Equals(resolved, StringComparison.OrdinalIgnoreCase)) return false;
        var physicalRoot = Path.TrimEndingDirectorySeparator(WindowsFileHandlePaths.FinalPath(directory, 2));
        // Both names come from normalized handles. Ordinal also protects case-sensitive
        // directories whose sibling names differ only in case.
        return actual.StartsWith(physicalRoot + '\\', StringComparison.Ordinal);
    }

    private static string NormalizeDosPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path[8..];
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        return Path.TrimEndingDirectorySeparator(path);
    }
}
