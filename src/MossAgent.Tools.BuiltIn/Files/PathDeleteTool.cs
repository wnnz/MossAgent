using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class PathDeleteTool(AuthorizedPathResolver paths, IPathDeleteSessionFactory? sessions = null) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.delete_path",
        ToolExecutionText.Get("DeleteDescription"),
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"},"recursive":{"type":"boolean"}}}""",
        ToolRiskLevel.HighRisk,
        ToolCapability.FileWrite);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        paths.RejectAuthorizedRootMutation(path, context);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        paths.RejectAuthorizedRootMutation(path, context);

        var recursive = request.Arguments.GetOptionalBoolean("recursive") ?? false;
        var count = await new PathDeletion(paths, sessions).DeleteAsync(path, recursive, null, context, cancellationToken);
        return ToolResult.Success(ToolExecutionText.Format("DeleteCompleted", count));
    }
}
