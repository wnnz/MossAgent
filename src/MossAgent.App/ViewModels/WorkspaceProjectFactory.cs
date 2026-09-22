using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceProjectFactory
{
    public static ProjectProfile Create(
        string name,
        string primaryDirectory,
        string authorizedDirectoriesText)
    {
        if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(primaryDirectory))
        {
            throw new InvalidOperationException("请填写项目名称和已经存在的目录。");
        }

        var primary = Path.GetFullPath(primaryDirectory);
        var authorized = authorizedDirectoriesText
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Prepend(primary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (authorized.Any(static path => !Directory.Exists(path)))
        {
            throw new InvalidOperationException("一个或多个授权目录不存在。");
        }

        return new ProjectProfile(
            Guid.NewGuid(), name.Trim(), primary, authorized, DateTimeOffset.UtcNow);
    }
}

