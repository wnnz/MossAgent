namespace MossAgent.Mcp;

internal sealed class McpReloadOperation(CancellationToken token, Action release) : IDisposable
{
    private Action? _release = release;
    public CancellationToken Token { get; } = token;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
