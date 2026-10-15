namespace MossAgent.Platform.Windows.Tests;

internal sealed class PtyPendingWriteStream : MemoryStream
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
        Entered.TrySetResult();
        await Release.Task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
