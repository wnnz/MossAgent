using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "写入/覆盖工作区内文件（自动创建目录）。参数: {\"path\": \"相对路径\", \"content\": \"内容\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var path = guard.ResolveInsideWorkspace(ToolArguments.GetString(argumentsJson, "path"));
            var content = ToolArguments.GetString(argumentsJson, "content");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(path, content, ct);
            return ToolResult.Ok($"已写入 {path}（{content.Length} 字符）");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolResult.Fail($"写入失败: {ex.Message}");
        }
    }
}
