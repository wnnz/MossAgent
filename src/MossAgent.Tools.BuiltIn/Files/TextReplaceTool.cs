using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class TextReplaceTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.replace_text",
        "在文本文件中精确替换唯一的一段文本。",
        """{"type":"object","required":["path","oldText","newText"],"properties":{"path":{"type":"string"},"oldText":{"type":"string"},"newText":{"type":"string"}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileRead | ToolCapability.FileWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var oldText = request.Arguments.GetRequiredString("oldText");
        var newText = request.Arguments.GetRequiredString("newText");
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var first = content.IndexOf(oldText, StringComparison.Ordinal);
        var last = content.LastIndexOf(oldText, StringComparison.Ordinal);

        if (first < 0)
        {
            return ToolResult.Failure("未找到要替换的文本。", "text_not_found");
        }

        if (first != last)
        {
            return ToolResult.Failure("目标文本出现多次，无法安全替换。", "text_not_unique");
        }

        var updated = string.Concat(content.AsSpan(0, first), newText, content.AsSpan(first + oldText.Length));
        await File.WriteAllTextAsync(path, updated, new UTF8Encoding(false), cancellationToken);
        return ToolResult.Success($"已更新文件：{path}");
    }
}
