using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>会话内消息。role=assistant 时 ToolCallsJson 保存待执行的工具调用；role=tool 时 ToolCallId 关联调用。</summary>
public class ChatMessage
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ToolCallsJson { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
