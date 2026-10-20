using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitStageFileTool(GitIndexPaths paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    internal const string Schema = """{"type":"object","additionalProperties":false,"required":["path"],"properties":{"repository":{"type":"string","minLength":1},"path":{"type":"string","minLength":1,"maxLength":32768},"originalPath":{"type":["string","null"],"minLength":1,"maxLength":32768}}}""";
    public ToolDescriptor Descriptor { get; } = new("git.stage_file", ToolExecutionText.Get("GitStageDescription"), Schema,
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => paths.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await repositories.AuthorizeAsync(paths.Directory(request, context), context, cancellationToken);
            var files = paths.ResolveFiles(request, context, repository.Root);
            return await git.RunAsync(repository.Root, repository.Command(["add", "--", .. files]), context, cancellationToken);
        }
        catch (TimeoutException)
        {
            return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout");
        }
    }
}
