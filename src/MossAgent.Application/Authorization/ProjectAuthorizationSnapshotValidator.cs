using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Authorization;

internal static class ProjectAuthorizationSnapshotValidator
{
    internal static ProjectProfile Capture(ProjectProfile project) => project with
    {
        AuthorizedDirectories = project.AuthorizedDirectories.ToArray(),
        RootAuthorizations = project.RootAuthorizations.ToArray()
    };

    internal static async Task ValidateAsync(IProjectAuthorizationReader reader, IProjectAuthorizationChanges changes,
        ProjectProfile expected, CancellationToken cancellationToken)
    {
        var current = await reader.ReadAuthorizationAsync(expected.Id, cancellationToken)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (expected.AuthorizationRevision <= 0 || current is null || current.AuthorizationRevision != expected.AuthorizationRevision
            || !comparer.Equals(current.PrimaryDirectory, expected.PrimaryDirectory)
            || !current.AuthorizedDirectories.SequenceEqual(expected.AuthorizedDirectories, comparer)
            || !current.RootAuthorizations.SequenceEqual(expected.RootAuthorizations))
        {
            if (current is not null && current.AuthorizationRevision > expected.AuthorizationRevision)
                changes.Publish(expected.Id, current.AuthorizationRevision);
            throw new WorkspaceAuthorizationException();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
