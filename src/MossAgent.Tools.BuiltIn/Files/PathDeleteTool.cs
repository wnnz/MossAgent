using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathDeleteTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.delete_path",
        "删除授权目录内的文件或目录；递归删除必须显式启用。",
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"},"recursive":{"type":"boolean"}}}""",
        ToolRiskLevel.HighRisk,
        ToolCapability.FileWrite);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        paths.RejectAuthorizedRootMutation(path, context);

        if (File.Exists(path))
        {
            File.Delete(path);
            return Task.FromResult(ToolResult.Success($"已删除文件：{path}"));
        }

        if (!Directory.Exists(path))
        {
            return Task.FromResult(ToolResult.Failure("目标路径不存在。", "path_not_found"));
        }

        var recursive = request.Arguments.GetOptionalBoolean("recursive") ?? false;
        Directory.Delete(path, recursive);
        return Task.FromResult(ToolResult.Success($"已删除目录：{path}"));
    }
}
