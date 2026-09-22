namespace MossAgent.Mcp;

public interface IMcpManager : IAsyncDisposable
{
    IReadOnlyDictionary<string, string> Errors { get; }
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

