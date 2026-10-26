using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestCreateTool(ISourcePullRequestWriter writer) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_create", ToolExecutionText.Get("SourceCreateDescription"),
        PullRequestInspectionRequest.CreateSchema(), ToolRiskLevel.ExternalSideEffect, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        writer.ValidateWriteAsync(WriteScope(context), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = request.Arguments;
            var body = arguments.TryGetProperty("body", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
            var receipt = await writer.CreateAsync(WriteScope(context),
                arguments.GetProperty("title").GetString()!,
                body,
                arguments.GetProperty("head").GetString()!,
                arguments.GetProperty("base").GetString()!,
                arguments.TryGetProperty("draft", out var flag) && flag.ValueKind == JsonValueKind.True,
                cancellationToken);
            return ToolResult.Success(ToolExecutionText.Get(receipt.Reconciled ? "SourceCreateLanded" : "SourceCreateSubmitted"),
                JsonSerializer.Serialize(receipt));
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.CreatePr(exception.ErrorCode); }
    }

    // The context scope must already carry write permission; the writer port re-reads the
    // live grant independently, so this check only adds fail-closed depth for stale contexts.
    private static SourceRepositoryScope WriteScope(ToolExecutionContext context)
    {
        var scope = PullRequestInspectionRequest.Scope(context);
        return scope.CanWrite ? scope : throw new SourceReviewException("source_scope_denied");
    }
}
