using System.Text.Json;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileFindTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.find_files",
        "按通配符查找授权目录中的文件，不跟随链接目录。",
        """{"type":"object","properties":{"path":{"type":"string"},"pattern":{"type":"string"},"recursive":{"type":"boolean"},"maximumResults":{"type":"integer","minimum":1,"maximum":5000}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.FileRead);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var root = paths.Resolve(
            request.Arguments.GetOptionalString("path") ?? context.WorkingDirectory,
            context);
        var pattern = request.Arguments.GetOptionalString("pattern") ?? "*";
        var maximum = Math.Clamp(
            request.Arguments.GetOptionalInt32("maximumResults") ?? 500,
            1,
            5000);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = request.Arguments.GetOptionalBoolean("recursive") ?? true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var results = new List<object>();

        foreach (var file in Directory.EnumerateFiles(root, pattern, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            results.Add(new { path = file, relativePath = Path.GetRelativePath(root, file), size = info.Length });
            if (results.Count >= maximum)
            {
                break;
            }
        }

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(ToolResult.Success($"找到 {results.Count} 个文件。", json));
    }
}
