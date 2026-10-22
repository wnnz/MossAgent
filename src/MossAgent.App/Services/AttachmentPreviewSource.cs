using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class AttachmentPreviewSource
{
    private readonly Func<CancellationToken, Task<AttachmentImage>> _load;
    private readonly IAttachmentThumbnailRenderer? _thumbnailRenderer;

    public AttachmentPreviewSource(AttachmentImage image, IAttachmentThumbnailRenderer renderer)
        : this("image", $"{image.ByteCount} B", image, renderer) { }

    public AttachmentPreviewSource(string displayPath, string sizeLabel, AttachmentImage image,
        IAttachmentThumbnailRenderer? renderer)
        : this(displayPath, sizeLabel, _ => Task.FromResult(image), renderer) { }

    internal AttachmentPreviewSource(string displayPath, string sizeLabel,
        Func<CancellationToken, Task<AttachmentImage>> load, IAttachmentThumbnailRenderer? renderer)
    {
        DisplayPath = string.IsNullOrWhiteSpace(displayPath) ? "attachment" : displayPath;
        SizeLabel = sizeLabel ?? string.Empty;
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _thumbnailRenderer = renderer;
    }

    public string DisplayPath { get; }
    public string SizeLabel { get; }

    public Task<AttachmentImage> LoadImageAsync(CancellationToken token) => _load(token);

    public Task<AttachmentImage> LoadThumbnailAsync(CancellationToken token) =>
        _thumbnailRenderer is null
            ? throw new InvalidOperationException("Attachment thumbnail rendering is unavailable.")
            : LoadThumbnailCoreAsync(token);

    public Task<AttachmentImage> LoadAsync(CancellationToken token) => LoadThumbnailAsync(token);

    private async Task<AttachmentImage> LoadThumbnailCoreAsync(CancellationToken token)
    {
        var image = await LoadImageAsync(token).WaitAsync(token).ConfigureAwait(false);
        return await _thumbnailRenderer!.RenderAsync(image, token).ConfigureAwait(false);
    }
}
