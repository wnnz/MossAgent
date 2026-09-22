using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCreateWorktreeTool(
    AuthorizedPathResolver paths,
    GitCommandRunner git) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git.create_worktree", "在授权目录内创建 Git worktree。",
        """{"type":"object","required":["path","branch"],"properties":{"repository":{"type":"string"},"path":{"type":"string"},"branch":{"type":"string"},"createBranch":{"type":"boolean"}}}""",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite | ToolCapability.Process);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory,
            context);
        var target = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var branch = request.Arguments.GetRequiredString("branch");
        var arguments = new List<string> { "worktree", "add" };
        if (request.Arguments.GetOptionalBoolean("createBranch") == true)
        {
            arguments.Add("-b");
            arguments.Add(branch);
            arguments.Add(target);
        }
        else
        {
            arguments.Add(target);
            arguments.Add(branch);
        }

        return git.RunAsync(repository, arguments, context, cancellationToken);
    }
}

