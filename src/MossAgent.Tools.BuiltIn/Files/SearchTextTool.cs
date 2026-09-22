using System.Diagnostics;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Processes;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class SearchTextTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.search_text", "使用 ripgrep 搜索授权目录中的文本。",
        """{"type":"object","required":["query"],"properties":{"query":{"type":"string"},"path":{"type":"string"},"glob":{"type":["string","null"]},"maximumMatches":{"type":"integer","minimum":1,"maximum":2000}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var query = request.Arguments.GetRequiredString("query");
        var path = paths.Resolve(
            request.Arguments.GetOptionalString("path") ?? context.WorkingDirectory,
            context);
        var maximum = Math.Clamp(request.Arguments.GetOptionalInt32("maximumMatches") ?? 500, 1, 2000);
        var startInfo = new ProcessStartInfo("rg")
        {
            WorkingDirectory = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in BuildArguments(request, query, maximum))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunAsync(startInfo, context.MaximumOutputCharacters, cancellationToken);
    }

    private static IEnumerable<string> BuildArguments(
        ToolRequest request,
        string query,
        int maximum)
    {
        yield return "--line-number";
        yield return "--column";
        yield return "--color=never";
        yield return $"--max-count={maximum}";
        var glob = request.Arguments.GetOptionalString("glob");
        if (!string.IsNullOrWhiteSpace(glob))
        {
            yield return "--glob";
            yield return glob;
        }

        yield return "--";
        yield return query;
        yield return ".";
    }

    private static async Task<ToolResult> RunAsync(
        ProcessStartInfo startInfo,
        int outputLimit,
        CancellationToken cancellationToken)
    {
        var output = new BoundedTextBuffer(outputLimit);
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, eventArgs) => output.AppendLine(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => output.AppendLine(eventArgs.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        return process.ExitCode is 0 or 1
            ? ToolResult.Success("搜索完成。", output.ToString())
            : ToolResult.Failure(output.ToString(), "search_failed");
    }
}
