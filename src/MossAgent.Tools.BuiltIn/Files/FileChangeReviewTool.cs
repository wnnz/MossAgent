using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileChangeReviewTool(AuthorizedPathResolver paths, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.review_change", FileToolText.Get("ReviewChangeDescription"),
        FileChangeRequest.InputSchema, ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process)
    {
        IsModelVisible = false
    };

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => FileChangeRequest.Validate(request, context, paths);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var change = FileChangeRequest.Validate(request, context, paths);
        var (before, after) = await new FileChangeSnapshotReader().ReadChangeAsync(change, context, cancellationToken);
        var patch = await new FileChangePatchBuilder(git).BuildAsync(change.Path, before, after, context, cancellationToken);
        try
        {
            paths.Resolve(change.Path, context);
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, FileToolText.Get("ReviewChangeSummary"), Artifacts: [patch]);
        }
        catch { FileChangeSnapshotStorage.Delete(Path.GetDirectoryName(patch.Path)!, Path.GetFileName(patch.Path)); throw; }
    }
}
