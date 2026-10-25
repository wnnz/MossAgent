using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

internal sealed class BrowserProfileApprovalRequest(Guid taskId, BrowserProfileMode mode)
{
    public Guid TaskId { get; } = taskId;
    public BrowserProfileMode Mode { get; } = mode;
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
