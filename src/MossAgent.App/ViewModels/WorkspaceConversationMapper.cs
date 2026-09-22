using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceConversationMapper
{
    public static ChatMessageViewModel ToChatMessage(ConversationMessage message) =>
        new(ToDisplayRole(message.Role), message.Content);

    public static ModelMessage ToModelMessage(ConversationMessage message) =>
        new(ToModelRole(message.Role), message.Content, message.ToolCallId);

    private static string ToDisplayRole(MessageRole role) =>
        role switch
        {
            MessageRole.User => "你",
            MessageRole.Assistant => "MossAgent",
            MessageRole.Tool => "工具",
            _ => "系统"
        };

    private static ModelRole ToModelRole(MessageRole role) =>
        role switch
        {
            MessageRole.User => ModelRole.User,
            MessageRole.Assistant => ModelRole.Assistant,
            MessageRole.Tool => ModelRole.Tool,
            _ => ModelRole.System
        };
}
