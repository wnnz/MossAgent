using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestCommentCreateTool(ISourcePullRequestWriter writer) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_comment_create", ToolExecutionText.Get("SourceCommentCreateDescription"),
        PullRequestInspectionRequest.CommentSchema(), ToolRiskLevel.ExternalSideEffect, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        writer.ValidateWriteAsync(WriteScope(context), PullRequestInspectionRequest.Revision(request, context), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await writer.CommentAsync(WriteScope(context), PullRequestInspectionRequest.Revision(request, context),
                request.Arguments.GetProperty("body").GetString()!, cancellationToken);
            return ToolResult.Success(ToolExecutionText.Get(receipt.Reconciled ? "SourceCommentLanded" : "SourceCommentCreated"),
                JsonSerializer.Serialize(receipt));
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.Create(exception.ErrorCode); }
    }

    // The context scope must already carry write permission; the writer port re-reads the
    // live grant independently, so this check only adds fail-closed depth for stale contexts.
    private static SourceRepositoryScope WriteScope(ToolExecutionContext context)
    {
        var scope = PullRequestInspectionRequest.Scope(context);
        return scope.CanWrite ? scope : throw new SourceReviewException("source_scope_denied");
    }
}
