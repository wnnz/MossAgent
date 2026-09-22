using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

public sealed class PolicyApprovalService(
    Func<ToolDescriptor, ToolRequest, CancellationToken, ValueTask<bool>> prompt)
    : IToolApprovalService
{
    public ValueTask<bool> IsApprovedAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        return context.ApprovalPolicy switch
        {
            ApprovalPolicy.ReadOnly => ValueTask.FromResult(
                descriptor.RiskLevel == ToolRiskLevel.ReadOnly),
            ApprovalPolicy.FullAccess => ValueTask.FromResult(
                descriptor.RiskLevel != ToolRiskLevel.HighRisk),
            _ when descriptor.RiskLevel == ToolRiskLevel.ReadOnly => ValueTask.FromResult(true),
            _ => prompt(descriptor, request, cancellationToken)
        };
    }
}

