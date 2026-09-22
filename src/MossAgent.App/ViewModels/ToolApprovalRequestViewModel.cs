using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal sealed class ToolApprovalRequestViewModel(ToolDescriptor descriptor)
{
    public string ToolName { get; } = descriptor.Name;
    public string Description { get; } = descriptor.Description;
    public TaskCompletionSource<bool> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
