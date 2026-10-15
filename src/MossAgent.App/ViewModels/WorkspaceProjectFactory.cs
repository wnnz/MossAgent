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
            throw new WorkspaceProjectValidationException(WorkspaceProjectValidationFailure.RequiredFields);
        }

        string[] authorized;
        try
        {
            authorized = directories.Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new WorkspaceProjectValidationException(WorkspaceProjectValidationFailure.InvalidDirectories);
        }
        if (authorized.Length == 0 || authorized.Any(static path => !Directory.Exists(path)))
        {
            throw new WorkspaceProjectValidationException(WorkspaceProjectValidationFailure.InvalidDirectories);
        }

        return new ProjectProfile(
            Guid.NewGuid(), name.Trim(), authorized[0], authorized, DateTimeOffset.UtcNow);
    }
}
