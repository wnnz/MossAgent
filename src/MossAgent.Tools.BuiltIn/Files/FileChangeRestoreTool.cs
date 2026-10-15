using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileChangeRestoreTool(AuthorizedPathResolver paths, AtomicTextFileWriter writer)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.restore_change", FileToolText.Get("RestoreChangeDescription"), FileChangeRequest.InputSchema,
        ToolRiskLevel.Mutation, ToolCapability.FileRead | ToolCapability.FileWrite)
    {
        IsModelVisible = false
    };

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        var change = FileChangeRequest.Validate(request, context, paths);
        if (change.BeforeSha256 is null) throw new ArgumentException();
        paths.RejectAuthorizedRootMutation(paths.Resolve(change.Path, context), context);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ValidateRequest(request, context);
        var change = context.ReviewedChange!;
        var (before, _) = await new FileChangeSnapshotReader().ReadChangeAsync(change, context, cancellationToken);
        var path = paths.Resolve(change.Path, context);
        var original = new FileSnapshot(change.AfterSha256?.ToUpperInvariant());
        var restored = await writer.WriteAsync(path, original, context,
            (stream, token) => stream.WriteAsync(before, token).AsTask(), cancellationToken);
        return ToolResult.Success(FileToolText.Get("RestoreChangeSummary")) with
        {
            FileChanges = [new(path, original.Sha256, restored.Sha256, change.AfterSnapshot, change.BeforeSnapshot)]
        };
    }
}
