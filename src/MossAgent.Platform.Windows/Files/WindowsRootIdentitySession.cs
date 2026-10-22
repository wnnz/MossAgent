using MossAgent.Domain;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsRootIdentitySession :
    IRootIdentitySession, IPathQuarantineSessionFactory
{
    private readonly object _gate = new();
    private readonly List<string> _paths = [];
    private readonly List<WindowsWriteDirectoryLease> _leases = [];
    private readonly bool _transferable;
    private bool _disposed;
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; }

    public WindowsRootIdentitySession(
        IReadOnlyList<string> roots, bool transferable, CancellationToken token)
    {
        _transferable = transferable && roots.Count == 1;
        var ordered = NormalizeRoots(roots);
        var identities = new DirectoryObjectIdentity?[roots.Count];
        try
        {
            foreach (var root in ordered)
            {
                token.ThrowIfCancellationRequested();
                var lease = transferable
                    ? WindowsWriteDirectoryLease.AcquireRoot(root.Path, token)
                    : WindowsWriteDirectoryLease.Acquire(
                        Path.Combine(root.Path, ".moss-root-identity"),
                        [root.Path], token, createParents: false);
                _leases.Add(lease);
                _paths.Add(root.Path);
                WindowsWriteDirectoryCasePolicy.Validate(lease.Parent);
                identities[root.Index] =
                    WindowsDirectoryObjectIdentity.Read(lease.Parent, root.Path);
            }
            token.ThrowIfCancellationRequested();
            Roots = Array.AsReadOnly(identities.Cast<DirectoryObjectIdentity>().ToArray());
        }
        catch { Dispose(); throw; }
    }

    private static IReadOnlyList<(string Path, int Index)> NormalizeRoots(
        IReadOnlyList<string> roots)
    {
        if (roots.Count is < 1 or > 64) throw new ProjectRootAuthorizationException();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<(string Path, int Index)>(roots.Count);
        for (var index = 0; index < roots.Count; index++)
        {
            if (!Path.IsPathFullyQualified(roots[index]))
                throw new ProjectRootAuthorizationException();
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(roots[index]));
            if (!paths.Add(path)) throw new ProjectRootAuthorizationException();
            normalized.Add((path, index));
        }
        return normalized
            .OrderBy(item => item.Path.Count(character => character == Path.DirectorySeparatorChar))
            .ThenBy(item => item.Path.Length)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_transferable)
            throw new NotSupportedException(
                "This root identity session was not opened for transfer.");
        if (!Path.IsPathFullyQualified(source) || !Path.IsPathFullyQualified(destination))
            throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
        var fullSource = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var fullDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (!string.Equals(Path.GetPathRoot(fullSource), Path.GetPathRoot(fullDestination),
                StringComparison.OrdinalIgnoreCase))
            throw new FileMutationSessionException(WindowsFileText.Get("MoveCrossVolume"),
                "file_move_cross_volume");
        if (!authorizedRoots.Select(Path.GetFullPath).Any(root =>
                fullDestination.StartsWith(
                    Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var index = _paths.FindIndex(path =>
                path.Equals(fullSource, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
            var lease = _leases[index];
            _leases.RemoveAt(index);
            _paths.RemoveAt(index);
            try
            {
                return WindowsPathQuarantineSession.FromPinnedRoot(
                    fullDestination, authorizedRoots.ToArray(), lease, cancellationToken);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var lease in _leases.AsEnumerable().Reverse()) lease.Dispose();
            _leases.Clear();
            _paths.Clear();
        }
    }
}
