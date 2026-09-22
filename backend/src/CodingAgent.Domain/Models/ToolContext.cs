namespace CodingAgent.Domain.Models;

/// <summary>工具执行上下文（会话与工作区信息）。</summary>
public class ToolContext
{
    public Guid SessionId { get; set; }
    public string WorkspacePath { get; set; } = string.Empty;
    /// <summary>本次运行允许的工具名；null 表示不限制。</summary>
    public IReadOnlyList<string>? AllowedToolNames { get; set; }
}
