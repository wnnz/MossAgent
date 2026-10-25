using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitStageHunksTool(GitHunkApplier hunks) : IAgentTool, IToolRequestPrevalidator
{
    internal const string Schema = """{"type":"object","additionalProperties":false,"required":["path","hunks","expectedDiff"],"properties":{"repository":{"type":"string","minLength":1},"path":{"type":"string","minLength":1,"maxLength":32768},"hunks":{"type":"array","minItems":1,"maxItems":64,"items":{"type":"integer","minimum":0,"maximum":999}},"expectedDiff":{"type":"string","pattern":"^[0-9a-f]{64}$"}}}""";
    public ToolDescriptor Descriptor { get; } = new("git.stage_hunks", ToolExecutionText.Get("GitStageHunksDescription"), Schema,
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => hunks.Validate(request, context);

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        hunks.ExecuteAsync(unstage: false, request, context, cancellationToken);
}
