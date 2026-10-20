using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitUnstageFileTool(GitIndexPaths paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.unstage_file", ToolExecutionText.Get("GitUnstageDescription"), GitStageFileTool.Schema,
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => paths.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await repositories.AuthorizeAsync(paths.Directory(request, context), context, cancellationToken);
            var files = paths.ResolveFiles(request, context, repository.Root);
            return await git.RunAsync(repository.Root, repository.Command(["reset", "--quiet", "--", .. files]), context, cancellationToken);
        }
        catch (TimeoutException)
        {
            return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout");
        }
    }
}
