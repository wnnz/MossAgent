using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class ListDirTool : ITool
{
    public string Name => "list_dir";
    public string Description => "列出工作区内目录内容（含子目录与文件大小）。参数: {\"path\": \"相对路径，可省略\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"path":{"type":"string","description":"目录相对路径，省略时为工作区根"}}}""";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var path = guard.ResolveInsideWorkspace(ToolArguments.GetString(argumentsJson, "path"));
            if (!Directory.Exists(path))
            {
                return Task.FromResult(ToolResult.Fail($"目录不存在: {path}"));
            }

            var entries = new List<string>();
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                entries.Add($"[dir]  {Path.GetFileName(dir)}");
            }
            foreach (var file in Directory.EnumerateFiles(path))
            {
                var size = new FileInfo(file).Length;
                entries.Add($"[file] {Path.GetFileName(file)} ({size} B)");
            }

            var relative = Path.GetRelativePath(guard.WorkspaceRoot, path);
            return Task.FromResult(ToolResult.Ok($"目录 {relative}\n{(entries.Count == 0 ? "(空)" : string.Join("\n", entries))}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(ToolResult.Fail($"列目录失败: {ex.Message}"));
        }
    }
}
