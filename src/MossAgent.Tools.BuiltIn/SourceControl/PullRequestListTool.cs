using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

public sealed class PullRequestListTool(ISourceReviewReader reader) : IAgentTool, IToolRequestAsyncPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("source.pr_list", ToolExecutionText.Get("SourceListDescription"),
        """{"type":"object","properties":{"page":{"type":"integer","minimum":1,"maximum":1000}},"required":["page"],"additionalProperties":false}""",
        ToolRiskLevel.ReadOnly, ToolCapability.Network) { IsModelVisible = false };

    public Task ValidateRequestAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        reader.ValidateAsync(context.SourceScope ?? throw new SourceReviewException("source_scope_denied"), cancellationToken);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var result = await reader.ListAsync(context.SourceScope ?? throw new SourceReviewException("source_scope_denied"),
                request.Arguments.GetProperty("page").GetInt32(), cancellationToken);
            return ToolResult.Success(ToolExecutionText.Get("SourceReadCompleted"), JsonSerializer.Serialize(result));
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.Create(exception.ErrorCode); }
    }
}
