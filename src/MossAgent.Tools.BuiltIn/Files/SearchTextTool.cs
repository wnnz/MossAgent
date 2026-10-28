using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Processes;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class SearchTextTool(AuthorizedPathResolver paths, ITaskProcessLauncher processes)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.search_text", "使用 ripgrep 搜索授权目录中的文本。",
        """{"type":"object","required":["query"],"properties":{"query":{"type":"string"},"path":{"type":"string"},"glob":{"type":["string","null"]},"maximumMatches":{"type":"integer","minimum":1,"maximum":2000}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(request.Arguments.GetOptionalString("path") ?? context.WorkingDirectory, context);
    }

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
        var startRequest = new TaskProcessStartRequest("rg", path,
            BuildArguments(request, query, maximum).ToArray(),
            Isolation: context.CreateProcessIsolation());
        return await RunAsync(startRequest, context.MaximumOutputCharacters, cancellationToken);
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

    private async Task<ToolResult> RunAsync(
        TaskProcessStartRequest startRequest,
        int outputLimit,
        CancellationToken cancellationToken)
    {
        var output = new BoundedTextBuffer(outputLimit);
        await using var process = processes.Start(startRequest, cancellationToken);
        var stdout = PumpAsync(process.StandardOutput, output, cancellationToken);
        var stderr = PumpAsync(process.StandardError, output, cancellationToken);
        var readers = Task.WhenAll(stdout, stderr);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            try { await process.TerminateAsync(); }
            finally { await readers.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
        }
        await readers;
        return process.ExitCode is 0 or 1
            ? ToolResult.Success("搜索完成。", output.ToString())
            : ToolResult.Failure(output.ToString(), "search_failed");
    }

    private static async Task PumpAsync(
        TextReader reader, BoundedTextBuffer output, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            output.Append(buffer.AsSpan(0, count));
    }
}
