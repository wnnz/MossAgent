using System.Collections.Concurrent;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;

namespace CodingAgent.Infrastructure.Mcp;

/// <summary>维护已连接的 MCP 连接（单例）。</summary>
public class McpConnectionRegistry(IMcpConnectionFactory factory)
{
    private readonly ConcurrentDictionary<int, IMcpConnection> _connections = new();

    public IMcpConnection GetOrConnect(McpServer server, CancellationToken ct = default)
    {
        return _connections.GetOrAdd(server.Id, _ =>
        {
            var connection = factory.Create(server);
            connection.InitializeAsync(ct).GetAwaiter().GetResult();
            return connection;
        });
    }

    public IMcpConnection? Get(int serverId) =>
        _connections.TryGetValue(serverId, out var connection) ? connection : null;

    public async Task DisconnectAsync(int serverId)
    {
        if (_connections.TryRemove(serverId, out var connection))
        {
            await connection.DisposeAsync();
        }
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var (id, connection) in _connections)
        {
            if (_connections.TryRemove(id, out _))
            {
                await connection.DisposeAsync();
            }
        }
    }
}
