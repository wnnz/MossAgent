using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileWriteTool(AuthorizedPathResolver paths, AtomicTextFileWriter? writer = null)
    : IAgentTool, IToolRequestPrevalidator
{
    private readonly AtomicTextFileWriter _writer = writer ?? new(paths);

    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.write_file",
        FileToolText.Get("WriteDescription"),
        """{"type":"object","required":["path","content"],"properties":{"path":{"type":"string"},"content":{"type":"string"},"expectedSha256":{"type":["string","null"]}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileWrite);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        FileSnapshotReader.ValidateExpectedHash(request.Arguments.GetOptionalString("expectedSha256"));
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var content = request.Arguments.GetRequiredString("content", allowEmpty: true);
        var expectedHash = request.Arguments.GetOptionalString("expectedSha256");
        var original = await FileSnapshotReader.CaptureForEditAsync(path, context, expectedHash, cancellationToken);
        var updated = await _writer.WriteTextWithHistoryAsync(path, content, new UTF8Encoding(false), original, context, cancellationToken);
        return ToolResult.Success(FileToolText.Format("WriteSummary", content.Length, path, updated.Version.Sha256!)) with
        {
            FileChanges = updated.Change is null ? null : [updated.Change]
        };
    }
}
