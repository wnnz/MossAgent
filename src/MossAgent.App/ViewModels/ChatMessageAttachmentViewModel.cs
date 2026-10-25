using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed record ChatMessageAttachmentViewModel(string DisplayPath, string SizeLabel,
    AttachmentThumbnailSource? Thumbnail = null, string KindLabel = "")
{
    public static string FormatSize(long byteCount) => byteCount < 1024
        ? $"{byteCount} B" : $"{byteCount / 1024d:0.#} KiB";
}
