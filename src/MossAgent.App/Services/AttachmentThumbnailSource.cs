using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class AttachmentThumbnailSource
{
    private readonly Func<CancellationToken, Task<AttachmentImage>> _load;

    public AttachmentThumbnailSource(AttachmentImage image, IAttachmentThumbnailRenderer renderer)
        : this(token => renderer.RenderAsync(image, token)) { }

    internal AttachmentThumbnailSource(Func<CancellationToken, Task<AttachmentImage>> load) => _load = load;

    public Task<AttachmentImage> LoadAsync(CancellationToken token) => _load(token);
}
