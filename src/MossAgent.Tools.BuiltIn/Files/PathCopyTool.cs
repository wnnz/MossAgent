using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathCopyTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.copy_path",
        "在授权目录之间复制文件或目录，目录复制不会跟随链接。",
        """{"type":"object","required":["source","destination"],"properties":{"source":{"type":"string"},"destination":{"type":"string"},"overwrite":{"type":"boolean"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var source = paths.Resolve(request.Arguments.GetRequiredString("source"), context);
        var destination = paths.Resolve(request.Arguments.GetRequiredString("destination"), context);
        var overwrite = request.Arguments.GetOptionalBoolean("overwrite") ?? false;

        if (File.Exists(source))
        {
            await CopyFileAsync(source, destination, overwrite, cancellationToken);
        }
        else if (Directory.Exists(source))
        {
            await CopyDirectoryAsync(source, destination, overwrite, cancellationToken);
        }
        else
        {
            return ToolResult.Failure("源路径不存在。", "path_not_found");
        }

        return ToolResult.Success($"已复制：{source} -> {destination}");
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        bool overwrite,
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
                    overwrite, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("无法确定目标父目录。"));
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destination, overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken);
    }
}
