using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Attachments;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAttachmentViewModel
{
    public WorkspaceAttachmentViewModel(
        WorkspaceAttachmentContent content,
        Action<WorkspaceAttachmentViewModel> remove)
    {
        Content = content;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    internal WorkspaceAttachmentContent Content { get; }

    public string DisplayPath => Content.DisplayPath;

    public string SizeLabel => Content.ByteCount < 1024
        ? $"{Content.ByteCount} B"
        : $"{Content.ByteCount / 1024d:0.#} KiB";

    public IRelayCommand RemoveCommand { get; }
}
