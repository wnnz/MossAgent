using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record ChatMessageAttachmentViewModel(string DisplayPath, string SizeLabel,
    AttachmentThumbnailSource? Thumbnail = null, string KindLabel = "", ConversationAttachment? Attachment = null)
{
    public static string FormatSize(long byteCount) => byteCount < 1024
        ? $"{byteCount} B" : $"{byteCount / 1024d:0.#} KiB";
}
