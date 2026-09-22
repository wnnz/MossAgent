using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Mcp;

/// <summary>MCP 连接工厂。</summary>
public class McpConnectionFactory : IMcpConnectionFactory
{
    public IMcpConnection Create(McpServer server) => server.Transport switch
    {
        Domain.Enums.McpTransport.Stdio => new StdioMcpConnection(server),
        Domain.Enums.McpTransport.Http => new HttpMcpConnection(server),
        _ => throw new NotSupportedException($"不支持的 MCP 传输: {server.Transport}"),
    };
}
