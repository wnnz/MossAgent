using System.Text.Json;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class DirectoryListTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.list_directory",
        "列出授权目录中的文件和子目录。",
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string"},"maximumEntries":{"type":"integer","minimum":1,"maximum":2000}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.FileRead);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var maximum = Math.Clamp(
            request.Arguments.GetOptionalInt32("maximumEntries") ?? 500,
            1,
            2000);
        var entries = Directory.EnumerateFileSystemEntries(path)
            .Take(maximum)
            .Select(entry => new
            {
                name = Path.GetFileName(entry),
                path = entry,
                kind = Directory.Exists(entry) ? "directory" : "file"
            });
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(ToolResult.Success($"已列出目录：{path}", json));
    }
}
