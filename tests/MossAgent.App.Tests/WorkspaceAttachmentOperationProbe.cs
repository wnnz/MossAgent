using MossAgent.App.Services;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceAttachmentOperationProbe : IFilePickerService, IWorkspaceAttachmentReader
{
    public TaskCompletionSource<IReadOnlyList<string>> PickerCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IReadOnlyList<WorkspaceAttachmentContent>> ReaderCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HoldPicker { get; set; }
    public bool HoldReader { get; set; }
    public Exception? PickerError { get; set; }
    public Exception? ReaderError { get; set; }
    public IReadOnlyList<string> Paths { get; set; } = [];
    public IReadOnlyList<WorkspaceAttachmentContent> Contents { get; set; } = [];
    public IReadOnlyList<string> ReadPaths { get; private set; } = [];
    public ProjectProfile? ReadProject { get; private set; }
    public CancellationToken PickerToken { get; private set; }
    public CancellationToken ReaderToken { get; private set; }
    public int PickerCalls { get; private set; }
    public int ReaderCalls { get; private set; }

    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken)
    {
        PickerCalls++;
        PickerToken = cancellationToken;
        return PickerError is not null ? Task.FromException<IReadOnlyList<string>>(PickerError)
            : HoldPicker ? PickerCompletion.Task : Task.FromResult(Paths);
    }

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(
        ProjectProfile project, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ReaderCalls++;
        ReaderToken = cancellationToken;
        ReadPaths = paths;
        ReadProject = project;
        return ReaderError is not null ? Task.FromException<IReadOnlyList<WorkspaceAttachmentContent>>(ReaderError)
            : HoldReader ? ReaderCompletion.Task : Task.FromResult(Contents);
    }

    public void SetContent(ProjectProfile project, string marker = "synthetic-attachment-owner-marker")
    {
        var path = Path.Combine(project.PrimaryDirectory, "attachment.txt");
        Paths = [path];
        Contents = [new(path, "attachment.txt", marker, marker.Length)];
    }

    public void Release()
    {
        PickerCompletion.TrySetResult(Paths);
        ReaderCompletion.TrySetResult(Contents);
    }
}
