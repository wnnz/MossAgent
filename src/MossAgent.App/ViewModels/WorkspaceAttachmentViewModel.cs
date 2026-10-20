using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Attachments;
using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAttachmentViewModel
{
    public WorkspaceAttachmentViewModel(
        WorkspaceAttachmentContent content,
        Action<WorkspaceAttachmentViewModel> remove,
        Guid? selectionId = null, string? authorizationStamp = null, bool isAvailable = true,
        IAttachmentThumbnailRenderer? thumbnailRenderer = null)
    {
        Content = content;
        SelectionId = selectionId ?? Guid.NewGuid();
        AuthorizationStamp = authorizationStamp ?? string.Empty;
        IsAvailable = isAvailable;
        RemoveCommand = new RelayCommand(() => remove(this));
        Thumbnail = isAvailable && content.Image is { } image && thumbnailRenderer is not null
            ? new AttachmentThumbnailSource(image, thumbnailRenderer) : null;
    }

    internal WorkspaceAttachmentContent Content { get; }
    internal Guid SelectionId { get; }
    internal string AuthorizationStamp { get; }
    public bool IsAvailable { get; }
    public AttachmentThumbnailSource? Thumbnail { get; }

    public string DisplayPath => Content.DisplayPath;
    public string FullPath => Content.FullPath;

    public string SizeLabel => IsAvailable ? ChatMessageAttachmentViewModel.FormatSize(Content.ByteCount)
        : WorkspaceText.Get("DraftAttachmentUnavailableLabel");

    public IRelayCommand RemoveCommand { get; }
}
