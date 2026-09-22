using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class DirectoryCreateTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.create_directory",
        "在授权目录内创建目录及缺失的父目录。",
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileWrite);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        Directory.CreateDirectory(path);
        return Task.FromResult(ToolResult.Success($"已创建目录：{path}"));
    }
}
