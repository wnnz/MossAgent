using System.Text.RegularExpressions;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class GrepTool : ITool
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int MaxMatches = 200;

    public string Name => "grep";
    public string Description => "正则搜索工作区文件内容。参数: {\"pattern\": \"正则\", \"path\": \"可选目录\", \"glob\": \"可选文件过滤如 *.cs\", \"ignore_case\": true}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"pattern":{"type":"string"},"path":{"type":"string"},"glob":{"type":"string"},"ignore_case":{"type":"boolean"}},"required":["pattern"]}""";

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
            var basePath = ToolArguments.GetString(argumentsJson, "path");
            var root = basePath.Length > 0 ? guard.ResolveInsideWorkspace(basePath) : guard.WorkspaceRoot;
            var glob = ToolArguments.GetString(argumentsJson, "glob");
            var ignoreCase = ToolArguments.GetBool(argumentsJson, "ignore_case", true);

            var regex = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase | RegexOptions.Compiled : RegexOptions.Compiled, TimeSpan.FromSeconds(5));
            var files = Directory.EnumerateFiles(root, glob.Length > 0 ? glob : "*.*", SearchOption.AllDirectories);

            var results = new List<string>();
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                if (info.Length > MaxFileBytes || IsBinary(info.FullName))
                {
                    continue;
                }
                var relative = Path.GetRelativePath(guard.WorkspaceRoot, file);
                foreach (var (lineNo, line) in ReadLines(info.FullName, ct))
                {
                    if (regex.IsMatch(line))
                    {
                        results.Add($"{relative}:{lineNo}: {line.Trim()}");
                        if (results.Count >= MaxMatches)
                        {
                            return Task.FromResult(ToolResult.Ok(string.Join("\n", results) + $"\n[已达 {MaxMatches} 条上限]"));
                        }
                    }
                }
            }

            return Task.FromResult(ToolResult.Ok(results.Count == 0 ? "无匹配" : string.Join("\n", results)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            return Task.FromResult(ToolResult.Fail($"搜索失败: {ex.Message}"));
        }
    }

    private static IEnumerable<(int LineNo, string Line)> ReadLines(string path, CancellationToken ct)
    {
        var lineNo = 0;
        foreach (var line in File.ReadLines(path))
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;
            yield return (lineNo, line);
        }
    }

    private static bool IsBinary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> buffer = stackalloc byte[512];
            var read = stream.Read(buffer);
            return buffer[..read].Contains((byte)0);
        }
        catch (IOException)
        {
            return true;
        }
    }
}
