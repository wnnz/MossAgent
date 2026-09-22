using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Models;

namespace CodingAgent.Domain.Interfaces;

/// <summary>MCP 连接：最小 JSON-RPC 2.0 客户端（initialize → tools/list → tools/call）。</summary>
public interface IMcpConnection : IAsyncDisposable
{
    int ServerId { get; }
    string ServerName { get; }
    bool IsConnected { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task<List<McpToolInfo>> ListToolsAsync(CancellationToken ct = default);
    Task<string> CallToolAsync(string toolName, string argumentsJson, CancellationToken ct = default);
}

public interface IMcpConnectionFactory
{
    IMcpConnection Create(McpServer server);
}
