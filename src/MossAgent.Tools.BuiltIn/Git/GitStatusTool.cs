using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitStatusTool(
    AuthorizedPathResolver paths,
    GitCommandRunner git) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git.status", "查看 Git 工作区状态。",
        """{"type":"object","properties":{"repository":{"type":"string"},"nullTerminated":{"type":"boolean"}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory,
            context);
        var arguments = new List<string>
        {
            "-c", "core.quotepath=false", "status", "--short", "--branch"
        };
        if (request.Arguments.GetOptionalBoolean("nullTerminated") == true)
        {
            arguments.Add("-z");
        }

        return git.RunAsync(repository, arguments, context, cancellationToken);
    }
}
