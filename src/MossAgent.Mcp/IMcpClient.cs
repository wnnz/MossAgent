using System.Text.Json;

namespace MossAgent.Mcp;

public interface IMcpClient : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);
    Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken);
}

