using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

public sealed class ProjectRootEnrollment : IDisposable
{
    private readonly IRootIdentitySession? _session;
    public IReadOnlyList<ProjectRootAuthorization> Authorizations { get; }

    private ProjectRootEnrollment(IRootIdentitySession? session, IReadOnlyList<ProjectRootAuthorization> authorizations)
    {
        _session = session;
        Authorizations = authorizations;
    }

    public static async Task<ProjectRootEnrollment> PrepareAsync(IRootIdentitySessionFactory factory,
        ProjectProfile? current, ProjectProfile requested, bool confirm, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (requested.AuthorizedDirectories.Count == 0)
        {
            if (current is null || confirm) throw new ProjectRootAuthorizationException();
            return new(null, []);
        }
        var session = await RootIdentitySessionOperation.OpenAsync(factory, requested.AuthorizedDirectories, token).ConfigureAwait(false);
        try
        {
            if (session.Roots.Count != requested.AuthorizedDirectories.Count || session.Roots.Count == 0
                || session.Roots.Any(item => item is null || string.IsNullOrWhiteSpace(item.VolumeId) || string.IsNullOrWhiteSpace(item.FileId))
                || session.Roots.Select(item => Path.TrimEndingDirectorySeparator(item.Path)).Distinct(
                    OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != session.Roots.Count
                || !session.Roots.Select(item => item.Path).Zip(requested.AuthorizedDirectories).All(pair => SamePath(pair.First, pair.Second))
                || current is null && !requested.AuthorizedDirectories.Any(path => SamePath(path, requested.PrimaryDirectory)))
                throw new ProjectRootAuthorizationException();
            var authorizations = new List<ProjectRootAuthorization>();
            foreach (var identity in session.Roots)
            {
                var retained = current?.AuthorizedDirectories.Any(path => SamePath(path, identity.Path)) == true;
                var previous = current?.RootAuthorizations.SingleOrDefault(item => SamePath(item.Identity.Path, identity.Path));
                if (!confirm && retained && (previous is null || !SameObject(previous.Identity, identity)))
                    throw new ProjectRootAuthorizationException();
                authorizations.Add(!confirm && previous is not null ? previous : new(Guid.NewGuid(), identity));
            }
            token.ThrowIfCancellationRequested();
            return new(session, authorizations.ToArray());
        }
        catch { session.Dispose(); throw; }
    }

    public static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static bool SameObject(DirectoryObjectIdentity expected, DirectoryObjectIdentity current) =>
        SamePath(expected.Path, current.Path) && expected.VolumeId == current.VolumeId && expected.FileId == current.FileId;

    public void Dispose() => _session?.Dispose();
}
