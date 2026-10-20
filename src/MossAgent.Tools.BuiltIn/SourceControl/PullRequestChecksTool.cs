using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestChecksTool(ISourcePullRequestInspection reader) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_checks", ToolExecutionText.Get("SourceChecksDescription"),
        PullRequestInspectionRequest.Schema("[\"runs\",\"statuses\"]"), ToolRiskLevel.ReadOnly, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        reader.ValidateAsync(PullRequestInspectionRequest.Scope(context), PullRequestInspectionRequest.Revision(request, context), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var result = await reader.ChecksAsync(PullRequestInspectionRequest.Scope(context), PullRequestInspectionRequest.Revision(request, context),
                request.Arguments.GetProperty("kind").GetString()!, request.Arguments.GetProperty("page").GetInt32(), cancellationToken);
            return PullRequestInspectionRequest.Complete(result, context);
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.Create(exception.ErrorCode); }
    }
}
