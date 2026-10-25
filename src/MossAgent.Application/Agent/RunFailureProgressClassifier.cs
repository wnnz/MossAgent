using MossAgent.Domain;

namespace MossAgent.Application.Agent;

/// <summary>
/// 将失败原因与已观察到的运行输出映射为 <see cref="RunFailureProgress"/>。
/// 只依据类型化的失败类别和持久化消息角色分类，不解析原始错误或提供商文本。
/// </summary>
public static class RunFailureProgressClassifier
{
    public static RunFailureProgress Classify(string? code, bool emittedModelContent, bool emittedToolEvent) =>
        Classify(TaskFailureClassifier.FromCode(code), emittedModelContent, emittedToolEvent);

    public static RunFailureProgress Classify(TaskFailureReason? reason, bool emittedModelContent, bool emittedToolEvent)
    {
        if (emittedToolEvent) return RunFailureProgress.ToolsExecuted;
        return reason switch
        {
            TaskFailureReason.UnsupportedImageInput or TaskFailureReason.InvalidModelParameters
                or TaskFailureReason.UnsupportedReasoningEffort or TaskFailureReason.AttachmentHistory
                or TaskFailureReason.ModelContextCapacity or TaskFailureReason.WorkspaceAuthorizationChanged
                or TaskFailureReason.ProxyAuthentication or TaskFailureReason.ProviderConnection
                => RunFailureProgress.NotSubmitted,
            TaskFailureReason.ProviderHttp or TaskFailureReason.InvalidProviderResponse
                or TaskFailureReason.ProviderRateLimit or TaskFailureReason.ProviderBlocked
                or TaskFailureReason.ProviderOutputLimit or TaskFailureReason.ProviderAuthentication
                or TaskFailureReason.ProviderTimeout or TaskFailureReason.StreamLineLimit
                or TaskFailureReason.StreamEventLimit or TaskFailureReason.InvalidStreamEncoding
                => RunFailureProgress.PossiblySubmitted,
            TaskFailureReason.TurnLimit => emittedModelContent
                ? RunFailureProgress.PossiblySubmitted : RunFailureProgress.Unknown,
            _ => RunFailureProgress.Unknown
        };
    }

    /// <summary>
    /// 从持久化历史推导一次失败运行的输出：最后一条用户消息之后的工具消息计为已执行工具，
    /// 之后的非空助手消息计为已产生模型内容。更早轮次的记录不计入。
    /// </summary>
    public static RunFailureProgress Classify(TaskFailureReason? reason, IReadOnlyList<ConversationMessage> history)
    {
        var lastUserIndex = -1;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            if (history[index].Role == MessageRole.User) { lastUserIndex = index; break; }
        }
        var emittedModelContent = false;
        var emittedToolEvent = false;
        for (var index = lastUserIndex + 1; index < history.Count; index++)
        {
            var message = history[index];
            emittedToolEvent |= message.Role == MessageRole.Tool;
            emittedModelContent |= message.Role == MessageRole.Assistant && !string.IsNullOrWhiteSpace(message.Content);
        }
        return Classify(reason, emittedModelContent, emittedToolEvent);
    }
}
