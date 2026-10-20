using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ThumbnailTestRenderer : IAttachmentThumbnailRenderer
{
    internal static AttachmentImage Image => AttachmentImage.FromDecodedBytes("image/png", Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"), 1, 1);

    internal List<TaskCompletionSource<AttachmentImage>> Requests { get; } = [];
    internal List<CancellationToken> Tokens { get; } = [];

    public Task<AttachmentImage> RenderAsync(AttachmentImage image, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<AttachmentImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Requests.Add(completion);
        Tokens.Add(cancellationToken);
        return completion.Task;
    }
}
