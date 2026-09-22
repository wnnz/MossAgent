using CodingAgent.Domain.Models;

namespace CodingAgent.Agent;

/// <summary>上下文窗口管理：按 maxContextTokens 估算 token，滑动窗口裁剪旧消息、超长工具输出截断。</summary>
public class ContextWindowManager
{
    private const int CharsPerToken = 4;
    private const int MaxToolOutputChars = 8_000;

    public int EstimateTokens(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + CharsPerToken - 1) / CharsPerToken;

    public int EstimateTokens(IEnumerable<LlmMessage> messages) =>
        messages.Sum(m => EstimateTokens(m.Content) + EstimateTokens(JoinToolCalls(m.ToolCalls)) + 8);

    /// <summary>裁剪消息以适配上下文窗口：保留 system 与最近消息；超长工具输出截断为占位摘要。</summary>
    public List<LlmMessage> Trim(List<LlmMessage> messages, int maxContextTokens)
    {
        var result = new List<LlmMessage>(messages.Count);
        foreach (var m in messages)
        {
            result.Add(TruncateToolOutput(m));
        }

        if (EstimateTokens(result) <= maxContextTokens)
        {
            return result;
        }

        // 保留首条 system 与最近 60% 预算的消息，从最旧的开始丢弃（保留尾部完整）
        const int reserveTokens = 2000;
        var budget = Math.Max(maxContextTokens - reserveTokens, 1000);

        var system = result.Where(m => m.Role == "system").ToList();
        var rest = result.Where(m => m.Role != "system").ToList();

        // 丢弃过旧的 tool/assistant 交替段：保留后缀
        var kept = new List<LlmMessage>();
        var total = system.Sum(m => EstimateMessage(m));
        for (var i = rest.Count - 1; i >= 0; i--)
        {
            var cost = EstimateMessage(rest[i]);
            if (total + cost > budget && kept.Count > 8)
            {
                break;
            }
            total += cost;
            kept.Insert(0, rest[i]);
        }

        // 前面若被截断，插入占位说明
        if (kept.Count < rest.Count)
        {
            system.Add(new LlmMessage("system", $"[更早的 {rest.Count - kept.Count} 条消息已因上下文窗口限制被裁剪]"));
        }

        var trimmed = new List<LlmMessage>(system.Count + kept.Count);
        trimmed.AddRange(system);
        trimmed.AddRange(kept);
        return trimmed;
    }

    private int EstimateMessage(LlmMessage message) =>
        EstimateTokens(message.Content) + EstimateTokens(JoinToolCalls(message.ToolCalls)) + 8;

    /// <summary>超长工具输出截断为占位摘要。</summary>
    public LlmMessage TruncateToolOutput(LlmMessage message)
    {
        if (message.Role != "tool" || message.Content is null || message.Content.Length <= MaxToolOutputChars)
        {
            return message;
        }
        var truncated = message.Content[..MaxToolOutputChars];
        return new LlmMessage("tool", truncated + $"\n[工具输出过长已截断，原始长度 {message.Content.Length} 字符]")
        {
            ToolCallId = message.ToolCallId,
            Name = message.Name,
        };
    }

    private static string JoinToolCalls(List<LlmToolCall>? calls) =>
        calls is null ? string.Empty : string.Concat(calls.Select(c => c.Name + c.ArgumentsJson));
}
