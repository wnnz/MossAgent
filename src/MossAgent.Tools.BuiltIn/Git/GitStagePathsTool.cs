using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitStagePathsTool(GitIndexPaths paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    internal const string Schema = """{"type":"object","additionalProperties":false,"required":["paths"],"properties":{"repository":{"type":"string","minLength":1},"paths":{"type":"array","minItems":1,"maxItems":64,"items":{"type":"string","minLength":1,"maxLength":32768}}}}""";
    public ToolDescriptor Descriptor { get; } = new("git.stage_paths", ToolExecutionText.Get("GitStagePathsDescription"), Schema,
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => paths.ValidatePaths(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await repositories.AuthorizeAsync(paths.Directory(request, context), context, cancellationToken);
            var files = paths.ResolveFileList(request, context, repository.Root);
            return await git.RunAsync(repository.Root, repository.Command(["add", "--", .. files]), context, cancellationToken);
        }
        catch (TimeoutException)
        {
            return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout");
        }
    }
}
