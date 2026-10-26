using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestMergeTool(ISourcePullRequestWriter writer) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_merge", ToolExecutionText.Get("SourceMergeDescription"),
        PullRequestInspectionRequest.MergeSchema(), ToolRiskLevel.ExternalSideEffect, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        writer.ValidateWriteAsync(WriteScope(context), PullRequestInspectionRequest.Revision(request, context), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await writer.MergeAsync(WriteScope(context),
                PullRequestInspectionRequest.Revision(request, context), Method(request), cancellationToken);
            return ToolResult.Success(ToolExecutionText.Get(receipt.Reconciled ? "SourceMergeLanded" : "SourceMergeSubmitted"),
                JsonSerializer.Serialize(receipt));
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.CreateMerge(exception.ErrorCode); }
    }

    // The context scope must already carry write permission; the writer port re-reads the
    // live grant independently, so this check only adds fail-closed depth for stale contexts.
    private static SourceRepositoryScope WriteScope(ToolExecutionContext context)
    {
        var scope = PullRequestInspectionRequest.Scope(context);
        return scope.CanWrite ? scope : throw new SourceReviewException("source_scope_denied");
    }

    private static PullRequestMergeMethod Method(ToolRequest request) =>
        request.Arguments.GetProperty("method").GetString() switch
        {
            "merge" => PullRequestMergeMethod.Merge,
            "squash" => PullRequestMergeMethod.Squash,
            "rebase" => PullRequestMergeMethod.Rebase,
            _ => throw new SourceReviewException("invalid_arguments")
        };
}
