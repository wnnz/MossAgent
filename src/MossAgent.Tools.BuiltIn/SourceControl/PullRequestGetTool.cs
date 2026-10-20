using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestGetTool(ISourceReviewReader reader) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_get", ToolExecutionText.Get("SourceGetDescription"),
        """{"type":"object","properties":{"number":{"type":"integer","minimum":1,"maximum":2147483647}},"required":["number"],"additionalProperties":false}""",
        ToolRiskLevel.ReadOnly, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        _ = PullRequestInspectionRequest.Number(request, context);
        return reader.ValidateAsync(PullRequestInspectionRequest.Scope(context), cancellationToken);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var result = await reader.GetAsync(context.SourceScope ?? throw new SourceReviewException("source_scope_denied"),
                PullRequestInspectionRequest.Number(request, context), cancellationToken);
            if (context.SourceReviewRevision is { } expected && !expected.Matches(result))
                throw new SourceReviewException("source_revision_changed");
            var output = ToolResult.Success(ToolExecutionText.Get("SourceReadCompleted"), JsonSerializer.Serialize(result));
            return context.SourceReviewRevision is null || output.OutputCharacterCount <= context.MaximumOutputCharacters ? output
                : SourceReadFailure.Create("source_output_limit");
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.Create(exception.ErrorCode); }
    }
}
