using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestReviewSubmitTool(ISourcePullRequestWriter writer) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_review_submit", ToolExecutionText.Get("SourceReviewSubmitDescription"),
        PullRequestInspectionRequest.ReviewSchema(), ToolRiskLevel.ExternalSideEffect, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        writer.ValidateWriteAsync(WriteScope(context), PullRequestInspectionRequest.Revision(request, context), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await writer.ReviewAsync(WriteScope(context), PullRequestInspectionRequest.Revision(request, context),
                Decision(request), Body(request), cancellationToken);
            return ToolResult.Success(ToolExecutionText.Get(receipt.Reconciled ? "SourceReviewLanded" : "SourceReviewSubmitted"),
                JsonSerializer.Serialize(receipt));
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.CreateReview(exception.ErrorCode); }
    }

    // The context scope must already carry write permission; the writer port re-reads the
    // live grant independently, so this check only adds fail-closed depth for stale contexts.
    private static SourceRepositoryScope WriteScope(ToolExecutionContext context)
    {
        var scope = PullRequestInspectionRequest.Scope(context);
        return scope.CanWrite ? scope : throw new SourceReviewException("source_scope_denied");
    }

    private static PullRequestReviewDecision Decision(ToolRequest request) =>
        request.Arguments.GetProperty("decision").GetString() switch
        {
            "approve" => PullRequestReviewDecision.Approve,
            "request_changes" => PullRequestReviewDecision.RequestChanges,
            "comment" => PullRequestReviewDecision.Comment,
            _ => throw new SourceReviewException("invalid_arguments")
        };

    private static string? Body(ToolRequest request) =>
        request.Arguments.TryGetProperty("body", out var value) ? value.GetString() : null;
}
