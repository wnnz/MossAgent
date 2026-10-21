using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileReadTool(AuthorizedPathResolver paths) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.read_file",
        FileToolText.Get("ReadDescription"),
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"},"offset":{"type":"integer","minimum":0},"length":{"type":"integer","minimum":1,"maximum":1048576}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.FileRead);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(request.Arguments.GetRequiredString("path"), context);
    }

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

        await using var stream = FileSnapshotReader.Open(path, context);
        var version = await FileSnapshotReader.HashAsync(stream, cancellationToken);
        stream.Seek(request.Arguments.GetOptionalInt64("offset") ?? 0, SeekOrigin.Begin);
        var buffer = new char[length];
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        var content = new string(buffer, 0, read);
        paths.Resolve(path, context);
        context.FileSnapshots.Record(context.TaskId, FileSnapshotReader.CacheKey(path), version);
        return ToolResult.Success(FileToolText.Format("ReadSummary", read, path, version.Sha256!), content);
    }

}
