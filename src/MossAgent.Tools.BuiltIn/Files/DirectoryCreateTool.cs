using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class DirectoryCreateTool(AuthorizedPathResolver paths, IAuthorizedDirectoryCreator? directories = null)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.create_directory",
        ToolExecutionText.Get("DirectoryCreateDescription"),
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileWrite);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(request.Arguments.GetRequiredString("path"), context);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        if (directories is null)
        {
            Directory.CreateDirectory(path);
            return ToolResult.Success(ToolExecutionText.Format("DirectoryReady", path));
        }
        var count = await directories.CreateAsync(path, context.AuthorizedRoots, async token =>
        {
            token.ThrowIfCancellationRequested();
            await context.ValidateWorkspaceAsync(token);
            paths.Resolve(path, context);
        }, cancellationToken);
        return ToolResult.Success(ToolExecutionText.Format("DirectoryCreateCompleted", count, path));
    }
}
