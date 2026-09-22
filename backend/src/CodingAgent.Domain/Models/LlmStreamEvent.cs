namespace CodingAgent.Domain.Models;

public enum LlmStreamEventType
{
    Delta,
    ToolCall,
    Completed,
    Error,
}

/// <summary>LLM 流式事件：增量文本 / 工具调用 / 完成 / 错误。</summary>
public class LlmStreamEvent
{
    public LlmStreamEventType Type { get; set; }
    public string? Delta { get; set; }
    public LlmToolCall? ToolCall { get; set; }
    public string? FinishReason { get; set; }
    public string? Error { get; set; }

    public static LlmStreamEvent DeltaOf(string content) => new() { Type = LlmStreamEventType.Delta, Delta = content };
    public static LlmStreamEvent ToolCallOf(LlmToolCall call) => new() { Type = LlmStreamEventType.ToolCall, ToolCall = call };
    public static LlmStreamEvent CompletedOf(string? finishReason) => new() { Type = LlmStreamEventType.Completed, FinishReason = finishReason };
    public static LlmStreamEvent ErrorOf(string message) => new() { Type = LlmStreamEventType.Error, Error = message };
}
