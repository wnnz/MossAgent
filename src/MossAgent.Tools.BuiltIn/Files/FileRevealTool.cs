using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileRevealTool(AuthorizedPathResolver paths, IFileRevealService reveal) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.reveal_file", FileToolText.Get("RevealDescription"),
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string","minLength":1,"maxLength":32767}},"additionalProperties":false}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        if (request.Arguments.EnumerateObject().Count() != 1) throw new ArgumentException();
        RequireFile(request, context);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = RequireFile(request, context);
        try
        {
            await reveal.RevealAsync(path, () => RequireFile(request, context), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return ToolResult.Success(FileToolText.Get("RevealSummary"), string.Empty);
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception) { return ToolResult.Failure(FileToolText.Get("RevealFailed"), "reveal_failed"); }
    }

    private string RequireFile(ToolRequest request, ToolExecutionContext context)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        if (!File.Exists(path)) throw new FileNotFoundException();
        return path;
    }
}
