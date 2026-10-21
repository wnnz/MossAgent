using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsRootIdentitySession : IRootIdentitySession
{
    private readonly List<WindowsWriteDirectoryLease> _leases = [];
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; }

    public WindowsRootIdentitySession(IReadOnlyList<string> roots, CancellationToken token)
    {
        var identities = new List<DirectoryObjectIdentity>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (roots.Count is < 1 or > 64) throw new ProjectRootAuthorizationException();
            foreach (var root in roots)
            {
                token.ThrowIfCancellationRequested();
                if (!Path.IsPathFullyQualified(root)) throw new ProjectRootAuthorizationException();
                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                if (!paths.Add(fullPath)) throw new ProjectRootAuthorizationException();
                var lease = WindowsWriteDirectoryLease.Acquire(Path.Combine(fullPath, ".moss-root-identity"), [fullPath], token, false);
                _leases.Add(lease);
                WindowsWriteDirectoryCasePolicy.Validate(lease.Parent);
                identities.Add(WindowsDirectoryObjectIdentity.Read(lease.Parent, fullPath));
            }
            token.ThrowIfCancellationRequested();
            Roots = Array.AsReadOnly(identities.ToArray());
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (var lease in _leases.AsEnumerable().Reverse()) lease.Dispose();
        _leases.Clear();
    }
}
