using MossAgent.Application.Models;
using MossAgent.App.Services;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

internal static class WorkspaceConversationMapper
{
    public static ChatMessageViewModel ToChatMessage(ConversationMessage message) => ToChatMessage(message, null);

    public static ChatMessageViewModel ToChatMessage(ConversationMessage message, ConversationAttachmentThumbnailFactory? thumbnails)
    {
        if (message.Role == MessageRole.Tool
            && WorkspaceToolMessageSerializer.TryDeserialize(message.Content, out var tool))
        {
            return ChatMessageViewModel.CreateTool(tool!, message.ToolCallId, createdAt: message.CreatedAt);
        }
        var attachments = message.Attachments?.Select(attachment => new ChatMessageAttachmentViewModel(
            attachment.DisplayPath, ChatMessageAttachmentViewModel.FormatSize(attachment.ByteCount),
            thumbnails?.Create(message, attachment),
            attachment.MediaType == AttachmentDocument.PdfMediaType ? "PDF" : string.Empty)).ToArray();
        return new ChatMessageViewModel(ToDisplayRole(message.Role), message.Content, attachments: attachments, createdAt: message.CreatedAt);
    }

    public static ModelMessage ToModelMessage(ConversationMessage message) =>
        message.Role == MessageRole.Tool
            ? HistoricalToolObservation.Create(message.Content, message.ToolCallId)
            : new(ToModelRole(message.Role), message.Content);

    private static string ToDisplayRole(MessageRole role) =>
        role switch
        {
            MessageRole.User => UiText.T("你"),
            MessageRole.Assistant => "MossAgent",
            MessageRole.Tool => UiText.T("工具"),
            _ => UiText.T("系统")
        };

    private static ModelRole ToModelRole(MessageRole role) =>
        role switch
        {
            MessageRole.User => ModelRole.User,
            MessageRole.Assistant => ModelRole.Assistant,
            MessageRole.System => ModelRole.System,
            _ => throw new InvalidOperationException("Unknown conversation role.")
        };
}

