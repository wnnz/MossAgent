using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Models;

/// <summary>发送给 LLM 的消息。role=assistant 时可带 ToolCalls；role=tool 时 ToolCallId 关联结果。</summary>
public class LlmMessage
{
    public LlmMessage() { }

    public LlmMessage(string role, string? content)
    {
        Role = role;
        Content = content;
    }

    public string Role { get; set; } = "user";
    public string? Content { get; set; }
    public List<LlmToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
}
