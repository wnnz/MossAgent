using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class TextReplaceTool(AuthorizedPathResolver paths, AtomicTextFileWriter? writer = null)
    : IAgentTool, IToolRequestPrevalidator
{
    private readonly AtomicTextFileWriter _writer = writer ?? new(paths);

    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.replace_text",
        FileToolText.Get("ReplaceDescription"),
        """{"type":"object","required":["path","oldText","newText"],"properties":{"path":{"type":"string"},"oldText":{"type":"string","minLength":1},"newText":{"type":"string"},"expectedSha256":{"type":["string","null"]}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

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
        var oldText = request.Arguments.GetRequiredString("oldText", allowEmpty: true);
        var newText = request.Arguments.GetRequiredString("newText", allowEmpty: true);
        if (oldText.Length == 0) return ToolResult.Failure(FileToolText.Get("EmptySearch"), "invalid_arguments");
        var expectedHash = request.Arguments.GetOptionalString("expectedSha256");
        FileSnapshotReader.ValidateExpectedHash(expectedHash);
        TextFileSnapshot snapshot;
        try { snapshot = await FileSnapshotReader.ReadTextAsync(path, cancellationToken); }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            FileSnapshotReader.ValidateForEdit(path, context, new FileSnapshot(null), expectedHash);
            return ToolResult.Failure(FileToolText.Get("FileNotFound"), "file_not_found");
        }
        FileSnapshotReader.ValidateForEdit(path, context, snapshot.Version, expectedHash);
        var content = snapshot.Content;
        var first = content.IndexOf(oldText, StringComparison.Ordinal);
        var last = content.LastIndexOf(oldText, StringComparison.Ordinal);

        if (first < 0)
        {
            return ToolResult.Failure(FileToolText.Get("TextNotFound"), "text_not_found");
        }

        if (first != last)
        {
            return ToolResult.Failure(FileToolText.Get("TextNotUnique"), "text_not_unique");
        }

        var updated = string.Concat(content.AsSpan(0, first), newText, content.AsSpan(first + oldText.Length));
        var outcome = await _writer.WriteTextWithHistoryAsync(path, updated, snapshot.Encoding, snapshot.Version, context, cancellationToken);
        return ToolResult.Success(FileToolText.Format("ReplaceSummary", path, outcome.Version.Sha256!)) with
        {
            FileChanges = outcome.Change is null ? null : [outcome.Change]
        };
    }
}
