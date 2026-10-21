using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsAuthorizedDirectoryLease(WindowsWriteDirectoryLease lease, int createdCount)
    : IAuthorizedDirectoryLease
{
    public int CreatedCount => createdCount;
    public void Dispose() => lease.Dispose();
}
