using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class AuthorizedPathResolver
{
    public string Resolve(string path, ToolExecutionContext context)
    {
        var candidate = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, context.WorkingDirectory);

        var roots = context.AuthorizedRoots
            .Append(context.WorkingDirectory)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (!roots.Any(root => IsWithin(candidate, root)))
        {
            throw new UnauthorizedAccessException("目标路径不在任务授权目录中。");
        }

        RejectExistingLinkEscape(candidate, roots);
        return candidate;
    }

    public void RejectAuthorizedRootMutation(
        string resolvedPath,
        ToolExecutionContext context)
    {
        if (GetRoots(context).Any(root =>
                resolvedPath.Equals(
                    Path.TrimEndingDirectorySeparator(root),
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("不允许直接修改或删除授权根目录。");
        }
    }

    public static bool IsSameOrDescendant(string candidate, string root) =>
        IsWithin(Path.GetFullPath(candidate), Path.GetFullPath(root));

    private static IEnumerable<string> GetRoots(ToolExecutionContext context) =>
        context.AuthorizedRoots
            .Append(context.WorkingDirectory)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool IsWithin(string candidate, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        return candidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectExistingLinkEscape(string candidate, IEnumerable<string> roots)
    {
        var authorizedRoots = roots.ToArray();
        var current = candidate;
        while (!string.IsNullOrWhiteSpace(current))
        {
            FileSystemInfo? info = File.Exists(current)
                ? new FileInfo(current)
                : Directory.Exists(current) ? new DirectoryInfo(current) : null;
            if (info?.LinkTarget is not null)
            {
                var target = info.ResolveLinkTarget(true)?.FullName;
                if (target is not null && !authorizedRoots.Any(root => IsWithin(target, root)))
                {
                    throw new UnauthorizedAccessException("路径中的链接指向授权目录之外。");
                }
            }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }
    }
}
