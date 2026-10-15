using MossAgent.Tools.Abstractions;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

internal sealed class ToolApprovalRequestViewModel(
    ToolDescriptor descriptor, ToolRequest request, ToolExecutionContext? context)
{
    public Guid RequestId { get; } = Guid.NewGuid();
    public Guid? TaskId { get; } = context?.TaskId;
    public string ToolName { get; } = ToolApprovalSummaryFormatter.SafeText(descriptor.Name, 160);
    public string Description { get; } = ToolApprovalSummaryFormatter.SafeText(descriptor.Description);
    public string WorkingDirectory { get; } = ToolApprovalSummaryFormatter.SafeText(context?.WorkingDirectory);
    public string Summary { get; } = ToolApprovalSummaryFormatter.Format(descriptor, request);
    public ToolApprovalContentViewModel ContentPreview { get; } = new(descriptor.Name, request.Arguments);
    public string RiskLabel { get; } = ApprovalText.Get(Enum.IsDefined(descriptor.RiskLevel)
        ? descriptor.RiskLevel.ToString() : "UnknownRisk");
    public TaskCompletionSource<bool> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
