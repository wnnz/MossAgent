using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FileChangeSnapshotWriter(ToolExecutionContext context) : IDisposable
{
    private string? _directory;
    private bool _committed;

    public ToolFileSnapshot? Before { get; private set; }
    public ToolFileSnapshot? After { get; private set; }

    public async Task CaptureBeforeAsync(string path, FileSnapshot version, CancellationToken token)
    {
        if (context.ArtifactDirectory is null) return;
        _directory = FileChangeSnapshotStorage.DirectoryFor(context);
        if (version.Exists) Before = await FileChangeSnapshotStorage.CaptureAsync(path, version.Sha256!, _directory, token);
    }

    public async Task CaptureAfterAsync(string path, FileSnapshot version, CancellationToken token)
    {
        if (_directory is not null)
            After = await FileChangeSnapshotStorage.CaptureAsync(path, version.Sha256!, _directory, token);
    }

    public void Commit() => _committed = true;

    public void Dispose()
    {
        if (_committed || _directory is null) return;
        if (Before is not null) FileChangeSnapshotStorage.Delete(_directory, Before.FileName);
        if (After is not null) FileChangeSnapshotStorage.Delete(_directory, After.FileName);
    }
}
