using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;
using Microsoft.Extensions.FileSystemGlobbing;

namespace CodingAgent.Infrastructure.Tools;

public class GlobTool : ITool
{
    public string Name => "glob";
    public string Description => "按 glob 模式搜索工作区文件。参数: {\"pattern\": \"**/*.cs\", \"path\": \"可选基目录\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"pattern":{"type":"string"},"path":{"type":"string"}},"required":["pattern"]}""";

    public Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var pattern = ToolArguments.GetString(argumentsJson, "pattern");
            if (pattern.Length == 0)
            {
                return Task.FromResult(ToolResult.Fail("pattern 不能为空"));
            }
            var basePath = guard.ResolveInsideWorkspace(ToolArguments.GetString(argumentsJson, "path"));

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(pattern);
            var matches = matcher.GetResultsInFullPath(basePath).ToList();

            var output = matches
                .Select(m => Path.GetRelativePath(guard.WorkspaceRoot, m))
                .OrderBy(p => p)
                .ToList();
            return Task.FromResult(ToolResult.Ok(output.Count == 0 ? "无匹配文件" : string.Join("\n", output)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return Task.FromResult(ToolResult.Fail($"搜索失败: {ex.Message}"));
        }
    }
}
