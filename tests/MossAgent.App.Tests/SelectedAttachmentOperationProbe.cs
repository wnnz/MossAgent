using MossAgent.Application.Attachments;

namespace MossAgent.App.Tests;

internal sealed class SelectedAttachmentOperationProbe : ISelectedAttachmentReader
{
    public TaskCompletionSource<IReadOnlyList<WorkspaceAttachmentContent>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<WorkspaceAttachmentContent> Contents { get; set; } = [];
    public bool Hold { get; set; }
    public int Calls { get; private set; }
    public CancellationToken Token { get; private set; }
    public IReadOnlyList<string> Paths { get; private set; } = [];

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadSelectedAsync(IReadOnlyList<string> selectedAbsolutePaths, CancellationToken cancellationToken)
    {
        Calls++;
        Token = cancellationToken;
        Paths = selectedAbsolutePaths;
        return Hold ? Completion.Task : Task.FromResult(Contents);
    }
}
