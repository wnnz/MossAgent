using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileReadTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.read_file",
        "读取授权目录内文本文件的一段内容。",
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"},"offset":{"type":"integer","minimum":0},"length":{"type":"integer","minimum":1,"maximum":1048576}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.FileRead);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var length = Math.Clamp(
            request.Arguments.GetOptionalInt32("length") ?? 64 * 1024,
            1,
            1024 * 1024);

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Seek(request.Arguments.GetOptionalInt64("offset") ?? 0, SeekOrigin.Begin);
        var buffer = new char[length];
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        var content = new string(buffer, 0, read);
        return ToolResult.Success($"已读取 {read} 个字符：{path}", content);
    }

}
