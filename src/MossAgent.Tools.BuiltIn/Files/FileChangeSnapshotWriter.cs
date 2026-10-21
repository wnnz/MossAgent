using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FileChangeSnapshotWriter(ToolExecutionContext context, IArtifactFileSessionFactory? sessions = null) : IDisposable
{
    private readonly OwnedFileChangeSnapshots? _owned = sessions is null ? null : new(context, sessions);
    private string? _directory;
    private bool _committed;

    public ToolFileSnapshot? Before { get; private set; }
    public ToolFileSnapshot? After { get; private set; }

    public async Task CaptureBeforeAsync(string path, FileSnapshot version, CancellationToken token)
    {
        if (context.ArtifactDirectory is null) return;
        _directory = _owned is null ? FileChangeSnapshotStorage.DirectoryFor(context) : context.ArtifactDirectory;
        if (!version.Exists) return;
        if (_owned is null) Before = await FileChangeSnapshotStorage.CaptureAsync(path, version.Sha256!, _directory, context, token);
        else
        {
            await using var source = FileSnapshotReader.Open(path, context);
            Before = await _owned.CaptureAsync(source, version.Sha256!, token);
        }
    }

    public async Task CaptureAfterAsync(string path, FileSnapshot version, CancellationToken token)
    {
        if (_directory is not null && _owned is null)
            After = await FileChangeSnapshotStorage.CaptureAsync(path, version.Sha256!, _directory, context, token);
        else if (_directory is not null)
        {
            await using var source = FileSnapshotReader.Open(path, context);
            After = await _owned!.CaptureAsync(source, version.Sha256!, token);
        }
    }

    public async Task CaptureAfterAsync(FileStream source, FileSnapshot version, CancellationToken token)
    {
        if (_directory is null) return;
        context.ReadHandleValidator?.Validate(source, context.AuthorizedRoots);
        After = _owned is null ? await FileChangeSnapshotStorage.CaptureAsync(source, version.Sha256!, _directory, token)
            : await _owned.CaptureAsync(source, version.Sha256!, token);
    }

    public void Commit() { _owned?.Retain(); _committed = true; }

    public void Dispose()
    {
        if (_owned is not null) { _owned.Dispose(); return; }
        if (_committed || _directory is null) return;
        if (Before is not null) FileChangeSnapshotStorage.Delete(_directory, Before.FileName);
        if (After is not null) FileChangeSnapshotStorage.Delete(_directory, After.FileName);
    }
}
