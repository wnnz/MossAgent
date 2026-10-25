using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitRemotesTool(GitRemoteStateReader states) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.remotes", ToolExecutionText.Get("GitRemotesDescription"),
        """{"type":"object","additionalProperties":false,"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => states.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var report = await states.ReadAsync(request, context, cancellationToken).ConfigureAwait(false);
            return ToolResult.Success(ToolExecutionText.Get("GitRemotesReady"), JsonSerializer.Serialize(report, JsonSerializerOptions.Web));
        }
        catch (GitRemotesException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }
}
