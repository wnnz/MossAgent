namespace MossAgent.Infrastructure.Attachments;

public sealed class AttachmentSnapshotLease : IDisposable
{
    private Action? _release;

    internal AttachmentSnapshotLease(Action release) => _release = release;

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
