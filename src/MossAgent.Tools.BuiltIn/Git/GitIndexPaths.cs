using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitIndexPaths(AuthorizedPathResolver paths)
{
    public void Validate(ToolRequest request, ToolExecutionContext context)
    {
        var directory = Directory(request, context);
        Resolve(request.Arguments.GetRequiredString("path"), directory, context);
        if (request.Arguments.GetOptionalString("originalPath") is { } original) Resolve(original, directory, context);
    }

    public string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    public string[] ResolveFiles(ToolRequest request, ToolExecutionContext context, string root)
    {
        var directory = Directory(request, context);
        var files = new[] { request.Arguments.GetRequiredString("path"), request.Arguments.GetOptionalString("originalPath") };
        return files.Where(file => file is not null).Select(file =>
        {
            var resolved = Resolve(file!, directory, context);
            if (!AuthorizedPathResolver.IsSameOrDescendant(resolved, root)) Deny();
            return Path.GetRelativePath(root, resolved);
        }).Distinct(StringComparer.Ordinal).ToArray();
    }

    private string Resolve(string path, string directory, ToolExecutionContext context)
    {
        var supplied = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            var segments = supplied.Split(Path.DirectorySeparatorChar);
            if (segments.Any(segment => IsInvalidWindowsSegment(segment, Path.GetPathRoot(supplied)))) Deny();
        }
        var resolved = paths.Resolve(Path.GetFullPath(path, directory), context);
        if (System.IO.Directory.Exists(resolved) || resolved.Split(Path.DirectorySeparatorChar).Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase))) Deny();
        var current = resolved;
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) Deny(); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
        return resolved;
    }

    private static bool IsInvalidWindowsSegment(string segment, string? root)
    {
        if (segment is "." or "..") return false;
        if (segment.EndsWith('.') || segment.EndsWith(' ')) return true;
        if (segment.Contains(':') && !segment.Equals(root?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
        var stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
            && stem[3] is >= '1' and <= '9' or '¹' or '²' or '³';
    }

    private static void Deny() => throw new UnauthorizedAccessException(ToolExecutionText.Get("GitIndexPathDenied"));
}
