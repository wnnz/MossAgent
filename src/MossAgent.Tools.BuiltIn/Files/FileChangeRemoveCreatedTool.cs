using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileChangeRemoveCreatedTool(AuthorizedPathResolver paths, AtomicTextFileWriter writer)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.remove_created_file", FileToolText.Get("RemoveCreatedDescription"), FileChangeRequest.InputSchema,
        ToolRiskLevel.HighRisk, ToolCapability.FileRead | ToolCapability.FileWrite)
    {
        IsModelVisible = false
    };

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        var change = FileChangeRequest.Validate(request, context, paths);
        if (change.BeforeSha256 is not null) throw new ArgumentException();
        paths.RejectAuthorizedRootMutation(paths.Resolve(change.Path, context), context);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ValidateRequest(request, context);
        var change = context.ReviewedChange!;
        await new FileChangeSnapshotReader().ReadChangeAsync(change, context, cancellationToken);
        var path = paths.Resolve(change.Path, context);
        var original = new FileSnapshot(change.AfterSha256?.ToUpperInvariant());
        await writer.DeleteAsync(path, original, context, cancellationToken);
        return ToolResult.Success(FileToolText.Get("RemoveCreatedSummary")) with
        {
            FileChanges = [new(path, original.Sha256, null, change.AfterSnapshot, AfterDeleted: true)]
        };
    }
}
