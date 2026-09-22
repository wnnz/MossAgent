using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class EditFileTool : ITool
{
    public string Name => "edit_file";
    public string Description => "精确替换工作区内文件中的字符串。参数: {\"path\": \"相对路径\", \"old_string\": \"待替换\", \"new_string\": \"替换为\", \"replace_all\": false}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"path":{"type":"string"},"old_string":{"type":"string"},"new_string":{"type":"string"},"replace_all":{"type":"boolean"}},"required":["path","old_string","new_string"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var path = guard.ResolveInsideWorkspace(ToolArguments.GetString(argumentsJson, "path"));
            var oldString = ToolArguments.GetString(argumentsJson, "old_string");
            var newString = ToolArguments.GetString(argumentsJson, "new_string");
            var replaceAll = ToolArguments.GetBool(argumentsJson, "replace_all");

            if (!File.Exists(path))
            {
                return ToolResult.Fail($"文件不存在: {path}");
            }
            if (oldString.Length == 0)
            {
                return ToolResult.Fail("old_string 不能为空");
            }

            var content = await File.ReadAllTextAsync(path, ct);
            var count = CountOccurrences(content, oldString);
            if (count == 0)
            {
                return ToolResult.Fail("old_string 在文件中未找到");
            }
            if (count > 1 && !replaceAll)
            {
                return ToolResult.Fail($"old_string 出现 {count} 次，请提供更长的上下文或设置 replace_all=true");
            }

            content = replaceAll ? content.Replace(oldString, newString) : ReplaceFirst(content, oldString, newString);
            await File.WriteAllTextAsync(path, content, ct);
            return ToolResult.Ok($"已替换 {path} 中 {Math.Min(count, 1) + (replaceAll ? count - 1 : 0)} 处");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolResult.Fail($"编辑失败: {ex.Message}");
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), newValue, text.AsSpan(index + oldValue.Length));
    }
}
