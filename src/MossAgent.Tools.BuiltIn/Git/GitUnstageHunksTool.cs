using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitUnstageHunksTool(GitHunkApplier hunks) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.unstage_hunks", ToolExecutionText.Get("GitUnstageHunksDescription"), GitStageHunksTool.Schema,
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => hunks.Validate(request, context);

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        hunks.ExecuteAsync(unstage: true, request, context, cancellationToken);
}
