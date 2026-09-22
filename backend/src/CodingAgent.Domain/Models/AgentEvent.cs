namespace CodingAgent.Domain.Models;

public enum AgentEventType
{
    TurnStarted,
    MessageDelta,
    ToolCallStarted,
    ToolCallFinished,
    TurnCompleted,
    Cancelled,
    Error,
}

/// <summary>Agent 循环事件（SSE 负载）。</summary>
public class AgentEvent
{
    public AgentEventType Type { get; set; }
    public object? Data { get; set; }

    public static AgentEvent Of(AgentEventType type, object? data = null) => new() { Type = type, Data = data };
}
