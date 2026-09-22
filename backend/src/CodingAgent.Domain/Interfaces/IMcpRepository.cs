using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

public interface IMcpRepository
{
    Task<List<McpServer>> GetAllServersAsync(CancellationToken ct = default);
    Task<McpServer?> GetServerAsync(int id, CancellationToken ct = default);
    Task AddServerAsync(McpServer server, CancellationToken ct = default);
    Task UpdateServerAsync(McpServer server, CancellationToken ct = default);
    Task DeleteServerAsync(int id, CancellationToken ct = default);
    Task<List<McpTool>> GetToolsAsync(int serverId, CancellationToken ct = default);
    Task ReplaceToolsAsync(int serverId, IEnumerable<McpTool> tools, CancellationToken ct = default);
}
