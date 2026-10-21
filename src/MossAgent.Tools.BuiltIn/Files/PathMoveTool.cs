using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathMoveTool(AuthorizedPathResolver paths, IPathMoveSessionFactory? sessions = null,
    IAuthorizedDirectoryCreator? directories = null) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.move_path",
        ToolExecutionText.Get("MoveDescription"),
        """{"type":"object","required":["source","destination"],"properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        var source = paths.Resolve(request.Arguments.GetRequiredString("source"), context);
        var destination = paths.Resolve(request.Arguments.GetRequiredString("destination"), context);
        paths.RejectAuthorizedRootMutation(source, context);
        paths.RejectAuthorizedRootMutation(destination, context);
    }

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

        if (sessions is not null || directories is not null)
        {
            if (sessions is null || directories is null) throw new InvalidOperationException();
            return new PathMoveOperation(paths, sessions, directories).ExecuteAsync(source, destination, overwrite, context, cancellationToken);
        }

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

        context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(source));
        context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(destination));
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
