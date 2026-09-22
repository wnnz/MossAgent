using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitLogTool(
    AuthorizedPathResolver paths,
    GitCommandRunner git) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "git.log", "读取最近的 Git 提交记录。",
        """{"type":"object","properties":{"repository":{"type":"string"},"count":{"type":"integer","minimum":1,"maximum":100}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory,
            context);
        var count = Math.Clamp(request.Arguments.GetOptionalInt32("count") ?? 20, 1, 100);
        return git.RunAsync(
            repository,
            ["log", $"-{count}", "--date=iso-strict", "--pretty=format:%h%x09%ad%x09%an%x09%s"],
            context,
            cancellationToken);
    }
}

