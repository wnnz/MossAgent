namespace MossAgent.Infrastructure.Attachments;

public sealed class AttachmentSnapshotLeases
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _paths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public AttachmentSnapshotLease Acquire(string path)
    {
        var key = Path.GetFullPath(path);
        lock (_gate) _paths[key] = _paths.TryGetValue(key, out var count) ? count + 1 : 1;
        return new AttachmentSnapshotLease(() =>
        {
            lock (_gate)
            {
                if (!_paths.TryGetValue(key, out var count) || count <= 1) _paths.Remove(key);
                else _paths[key] = count - 1;
            }
        });
    }

    public bool IsLeased(string path)
    {
        var key = Path.GetFullPath(path);
        lock (_gate) return _paths.ContainsKey(key);
    }
}
