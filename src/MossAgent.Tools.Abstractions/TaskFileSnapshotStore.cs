namespace MossAgent.Tools.Abstractions;

public sealed class TaskFileSnapshotStore(Guid taskId)
{
    private const int MaximumEntries = 4096;
    private readonly object _gate = new();
    private readonly Dictionary<string, FileSnapshot> _snapshots = new(StringComparer.Ordinal);

    public FileSnapshot? Get(Guid ownerTaskId, string path)
    {
        ValidateOwner(ownerTaskId);
        lock (_gate) return _snapshots.GetValueOrDefault(path);
    }

    public void Record(Guid ownerTaskId, string path, FileSnapshot snapshot)
    {
        ValidateOwner(ownerTaskId);
        lock (_gate)
        {
            if (!_snapshots.ContainsKey(path) && _snapshots.Count >= MaximumEntries)
                throw new InvalidOperationException("Task file snapshot limit exceeded.");
            _snapshots[path] = snapshot;
        }
    }

    public void Forget(Guid ownerTaskId, string path)
    {
        ValidateOwner(ownerTaskId);
        lock (_gate)
        {
            var prefix = path.TrimEnd('/', '\\');
            foreach (var key in _snapshots.Keys.Where(key => key == prefix
                || key.StartsWith(prefix + "/", StringComparison.Ordinal)
                || key.StartsWith(prefix + "\\", StringComparison.Ordinal)).ToArray())
                _snapshots.Remove(key);
        }
    }

    private void ValidateOwner(Guid ownerTaskId)
    {
        if (ownerTaskId != taskId)
            throw new InvalidOperationException("File snapshots belong to a different task.");
    }
}
