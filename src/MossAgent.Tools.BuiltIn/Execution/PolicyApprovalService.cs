using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

public sealed class PolicyApprovalService : IToolApprovalService
{
    private readonly Func<ToolDescriptor, ToolRequest, ToolExecutionContext, CancellationToken, ValueTask<bool>> _prompt;

    public PolicyApprovalService(Func<ToolDescriptor, ToolRequest, CancellationToken, ValueTask<bool>> prompt)
        : this((descriptor, request, _, cancellationToken) => prompt(descriptor, request, cancellationToken))
    {
    }

    public PolicyApprovalService(
        Func<ToolDescriptor, ToolRequest, ToolExecutionContext, CancellationToken, ValueTask<bool>> prompt)
    {
        _prompt = prompt;
    }

    public ValueTask<bool> IsApprovedAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        return context.ApprovalPolicy switch
        {
            ApprovalPolicy.ReadOnly => ValueTask.FromResult(
                ToolCapabilityPolicy.IsAllowed(descriptor, ApprovalPolicy.ReadOnly)),
            ApprovalPolicy.FullAccess => ValueTask.FromResult(
                descriptor.RiskLevel != ToolRiskLevel.HighRisk),
            _ when descriptor.RiskLevel == ToolRiskLevel.ReadOnly
                && !ToolCapabilityPolicy.RequiresInteractiveApproval(descriptor) =>
                    ValueTask.FromResult(true),
            _ => _prompt(descriptor, request, context, cancellationToken)
        };
    }
}
