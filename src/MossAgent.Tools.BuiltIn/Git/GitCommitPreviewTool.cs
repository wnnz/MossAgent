using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommitPreviewTool(GitCommitStateReader states) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.commit_preview", ToolExecutionText.Get("GitCommitPreviewDescription"),
        """{"type":"object","additionalProperties":false,"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => states.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await states.CaptureAsync(request, context, cancellationToken).ConfigureAwait(false);
            return ToolResult.Success(ToolExecutionText.Get("GitCommitPreviewReady"), JsonSerializer.Serialize(snapshot.Preview, JsonSerializerOptions.Web));
        }
        catch (GitCommitException failure) { return ToolResult.Failure(failure.Message, failure.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }
}
