using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitDiffTool(
    AuthorizedPathResolver paths,
    GitCommandRunner git) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git.diff", "查看未暂存或已暂存的 Git Diff。",
        """{"type":"object","properties":{"repository":{"type":"string"},"staged":{"type":"boolean"},"path":{"type":["string","null"]}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory,
            context);
        var arguments = new List<string> { "diff", "--no-ext-diff", "--no-color" };
        if (request.Arguments.GetOptionalBoolean("staged") == true)
        {
            arguments.Add("--staged");
        }

        var path = request.Arguments.GetOptionalString("path");
        if (!string.IsNullOrWhiteSpace(path))
        {
            arguments.Add("--");
            arguments.Add(path);
        }

        return git.RunAsync(repository, arguments, context, cancellationToken);
    }
}

