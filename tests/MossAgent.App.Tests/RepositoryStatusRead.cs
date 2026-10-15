namespace MossAgent.App.Tests;

internal sealed class RepositoryStatusRead(string directory, CancellationToken token) : IDisposable
{
    public string Directory { get; } = directory;
    public CancellationToken Token { get; } = token;
    public TaskCompletionSource<string?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => Completion.TrySetResult(null);
}
