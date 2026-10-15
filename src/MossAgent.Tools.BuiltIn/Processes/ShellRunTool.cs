using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Processes;

public sealed class ShellRunTool(AuthorizedPathResolver paths, ITaskProcessLauncher processes)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "shell.run",
        ToolExecutionText.Get("CommandDescription"),
        """{"type":"object","required":["command"],"properties":{"command":{"type":"string"},"workingDirectory":{"type":["string","null"]}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        ResolveWorkingDirectory(request.Arguments.GetOptionalString("workingDirectory"), context);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var command = request.Arguments.GetRequiredString("command");
        var requestedDirectory = request.Arguments.GetOptionalString("workingDirectory");
        var workingDirectory = ResolveWorkingDirectory(requestedDirectory, context);
        var output = new BoundedTextBuffer(context.MaximumOutputCharacters);
        await using var process = processes.Start(CreateRequest(command, workingDirectory), cancellationToken);
        var standardOutput = ReadOutputAsync(process.StandardOutput, output, cancellationToken);
        var standardError = ReadOutputAsync(process.StandardError, output, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            await process.TerminateAsync();
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        await Task.WhenAll(standardOutput, standardError);
        var content = output.ToString();
        return process.ExitCode == 0
            ? ToolResult.Success(ToolExecutionText.Format("CommandCompleted", process.ExitCode), content)
            : new ToolResult(false, ToolExecutionText.Format("CommandFailed", process.ExitCode), content,
                ErrorCode: "process_failed");
    }

    private static async Task ReadOutputAsync(
        TextReader reader, BoundedTextBuffer output, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            output.Append(buffer.AsSpan(0, count));
    }

    private static TaskProcessStartRequest CreateRequest(string command, string workingDirectory)
    {
        var script = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); "
            + "$OutputEncoding = [Console]::OutputEncoding; & ([ScriptBlock]::Create('"
            + command.Replace("'", "''", StringComparison.Ordinal) + "'))";
        return new TaskProcessStartRequest("pwsh", workingDirectory,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script]);
    }

    private string ResolveWorkingDirectory(string? requested, ToolExecutionContext context) =>
        paths.Resolve(requested ?? context.WorkingDirectory, context);
}
