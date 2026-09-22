using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceProjectFactory
{
    public static ProjectProfile Create(
        string name,
        IReadOnlyList<string> directories)
    {
        if (string.IsNullOrWhiteSpace(name) || directories.Count == 0)
        {
            throw new InvalidOperationException("请填写项目名称并至少添加一个目录。");
        }

        var authorized = directories
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (authorized.Length == 0 || authorized.Any(static path => !Directory.Exists(path)))
        {
            throw new InvalidOperationException("一个或多个授权目录不存在。");
        }

        return new ProjectProfile(
            Guid.NewGuid(), name.Trim(), authorized[0], authorized, DateTimeOffset.UtcNow);
    }
}
