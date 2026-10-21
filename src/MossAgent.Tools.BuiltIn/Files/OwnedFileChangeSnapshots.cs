using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class OwnedFileChangeSnapshots(ToolExecutionContext context, IArtifactFileSessionFactory factory) : IDisposable
{
    private readonly List<IArtifactFileSession> _sessions = [];

    public async Task<ToolFileSnapshot?> CaptureAsync(FileStream source, string expectedHash, CancellationToken token)
    {
        if (source.Length > ToolFileSnapshot.MaximumBytes) return null;
        source.Position = 0;
        var before = await FileSnapshotReader.HashAsync(source, token);
        if (!string.Equals(before.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase)) throw new FileEditException("file_changed", "FileChanged");
        source.Position = 0;
        var session = await factory.CreateAsync(context, "edit", ".snapshot", token);
        try
        {
            await source.CopyToAsync(session.Stream, token);
            await session.Stream.FlushAsync(token);
            session.Stream.Position = 0;
            var after = await FileSnapshotReader.HashAsync(session.Stream, token);
            if (session.Stream.Length > ToolFileSnapshot.MaximumBytes || !string.Equals(after.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new FileEditException("file_changed", "FileChanged");
            await session.CompleteAsync(token);
            var result = new ToolFileSnapshot(Path.GetFileName(session.Path), session.Stream.Length, after.Sha256!);
            _sessions.Add(session);
            return result;
        }
        catch { session.Dispose(); throw; }
    }

    public void Retain()
    {
        foreach (var session in _sessions) session.Retain();
    }

    public void Dispose()
    {
        FileMutationSessionException? failure = null;
        foreach (var session in _sessions)
        {
            try { session.Dispose(); }
            catch (FileMutationSessionException exception) { failure ??= exception; }
        }
        if (failure is not null) throw failure;
    }
}
