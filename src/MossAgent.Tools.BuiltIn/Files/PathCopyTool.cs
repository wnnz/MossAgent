using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathCopyTool(AuthorizedPathResolver paths, AtomicTextFileWriter? writer = null,
    IFileTreeReadSessionFactory? sourceSessions = null, IAuthorizedDirectoryCreator? directories = null)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.copy_path",
        ToolExecutionText.Get("CopyDescription"),
        """{"type":"object","required":["source","destination"],"properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(request.Arguments.GetRequiredString("source"), context);
        paths.Resolve(request.Arguments.GetRequiredString("destination"), context);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var source = paths.Resolve(request.Arguments.GetRequiredString("source"), context);
        var destination = paths.Resolve(request.Arguments.GetRequiredString("destination"), context);
        var overwrite = request.Arguments.GetOptionalBoolean("overwrite") ?? false;
        if (sourceSessions is not null || writer is not null || directories is not null)
        {
            if (sourceSessions is null || writer is null || directories is null) throw new InvalidOperationException();
            return await new PathCopyOperation(paths, writer, sourceSessions, directories)
                .ExecuteAsync(source, destination, overwrite, context, cancellationToken);
        }

        if (File.Exists(source))
        {
            await CopyFileAsync(source, destination, overwrite, context, cancellationToken);
        }
        else if (Directory.Exists(source))
        {
            await CopyDirectoryAsync(source, destination, overwrite, context, cancellationToken);
        }
        else
        {
            return ToolResult.Failure("源路径不存在。", "path_not_found");
        }

        context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(destination));
        return ToolResult.Success($"已复制：{source} -> {destination}");
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        bool overwrite,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (AuthorizedPathResolver.IsSameOrDescendant(destination, source))
        {
            throw new IOException("不能将目录复制到自身内部。");
        }

        var pending = new Queue<(string Source, string Destination)>();
        pending.Enqueue((source, destination));
        var enumeration = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };

        while (pending.TryDequeue(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(current.Destination);
            foreach (var file in Directory.EnumerateFiles(current.Source, "*", enumeration))
            {
                await CopyFileAsync(
                    file, Path.Combine(current.Destination, Path.GetFileName(file)),
                    overwrite, context, cancellationToken);
            }

            foreach (var directory in Directory.EnumerateDirectories(current.Source, "*", enumeration))
            {
                pending.Enqueue((directory, Path.Combine(current.Destination, Path.GetFileName(directory))));
            }
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string destination,
        bool overwrite,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        await using var input = FileSnapshotReader.Open(source, context);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("无法确定目标父目录。"));
        await using var output = new FileStream(
            destination, overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken);
    }
}
