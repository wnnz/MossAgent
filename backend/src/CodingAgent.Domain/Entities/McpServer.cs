using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>MCP 服务器配置。</summary>
public class McpServer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public McpTransport Transport { get; set; }
    public string? Command { get; set; }
    /// <summary>JSON 数组：进程参数。</summary>
    public string? ArgsJson { get; set; }
    /// <summary>JSON 对象：进程环境变量。</summary>
    public string? EnvJson { get; set; }
    public string? Url { get; set; }
    /// <summary>JSON 对象：HTTP 请求头。</summary>
    public string? HeadersJson { get; set; }
    public bool Enabled { get; set; } = true;
    public bool AutoConnect { get; set; }
}
