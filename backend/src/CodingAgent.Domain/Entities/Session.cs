using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>聊天会话。</summary>
public class Session
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public ReasoningEffort ReasoningEffort { get; set; }
    public string? WorkspacePath { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
