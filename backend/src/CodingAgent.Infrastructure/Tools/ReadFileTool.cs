using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class ReadFileTool : ITool
{
    private const long MaxBytes = 256 * 1024;

    public string Name => "read_file";
    public string Description => "读取工作区内文件内容。参数: {\"path\": \"相对路径\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"path":{"type":"string","description":"文件相对路径"}},"required":["path"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var path = guard.ResolveInsideWorkspace(ToolArguments.GetString(argumentsJson, "path"));
            if (!File.Exists(path))
            {
                return ToolResult.Fail($"文件不存在: {path}");
            }
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return ToolResult.Fail($"文件过大（{info.Length} 字节），超出读取上限 {MaxBytes}");
            }
            var content = await File.ReadAllTextAsync(path, ct);
            return ToolResult.Ok(ToolArguments.Truncate(content));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolResult.Fail($"读取失败: {ex.Message}");
        }
    }
}
