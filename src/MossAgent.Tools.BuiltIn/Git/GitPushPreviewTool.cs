using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitPushPreviewTool(GitPushStateReader states) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.push_preview", ToolExecutionText.Get("GitPushPreviewDescription"),
        """{"type":"object","additionalProperties":false,"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"remote":{"type":"string","minLength":1,"maxLength":256},"targetBranch":{"type":"string","minLength":1,"maxLength":1024}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => states.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await states.CaptureAsync(request, context, cancellationToken).ConfigureAwait(false);
            return ToolResult.Success(ToolExecutionText.Get("GitPushPreviewReady"), JsonSerializer.Serialize(snapshot.Preview, JsonSerializerOptions.Web));
        }
        catch (GitPushException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }
}
