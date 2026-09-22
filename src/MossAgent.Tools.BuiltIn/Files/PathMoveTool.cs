using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathMoveTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.move_path",
        "在授权目录之间移动或重命名文件、目录。",
        """{"type":"object","required":["source","destination"],"properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = paths.Resolve(request.Arguments.GetRequiredString("source"), context);
        var destination = paths.Resolve(request.Arguments.GetRequiredString("destination"), context);
        paths.RejectAuthorizedRootMutation(source, context);
        var overwrite = request.Arguments.GetOptionalBoolean("overwrite") ?? false;

        if (File.Exists(source))
        {
            CreateParent(destination);
            File.Move(source, destination, overwrite);
        }
        else if (Directory.Exists(source))
        {
            MoveDirectory(source, destination);
        }
        else
        {
            return Task.FromResult(ToolResult.Failure("源路径不存在。", "path_not_found"));
        }

        return Task.FromResult(ToolResult.Success($"已移动：{source} -> {destination}"));
    }

    private static void MoveDirectory(string source, string destination)
    {
        if (AuthorizedPathResolver.IsSameOrDescendant(destination, source))
        {
            throw new IOException("不能将目录移动到自身内部。");
        }

        CreateParent(destination);
        Directory.Move(source, destination);
    }

    private static void CreateParent(string path)
    {
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定目标父目录。");
        Directory.CreateDirectory(parent);
    }
}
