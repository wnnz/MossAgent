using System.Diagnostics;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Processes;

public sealed class ShellRunTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "shell.run",
        "在授权工作目录中运行 PowerShell 命令。",
        """{"type":"object","required":["command"],"properties":{"command":{"type":"string"},"workingDirectory":{"type":["string","null"]}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Process);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var command = request.Arguments.GetRequiredString("command");
        var requestedDirectory = request.Arguments.GetOptionalString("workingDirectory");
        var workingDirectory = ResolveWorkingDirectory(requestedDirectory, context);
        var output = new BoundedTextBuffer(context.MaximumOutputCharacters);
        using var process = CreateProcess(command, workingDirectory);
        process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
        process.ErrorDataReceived += (_, args) => output.AppendLine(args.Data);

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
        var content = output.ToString();
        return process.ExitCode == 0
            ? ToolResult.Success($"命令执行完成，退出码 {process.ExitCode}。", content)
            : ToolResult.Failure($"命令执行失败，退出码 {process.ExitCode}。\n{content}", "process_failed");
    }

    private static Process CreateProcess(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);
        return new Process { StartInfo = startInfo };
    }

    private static string ResolveWorkingDirectory(string? requested, ToolExecutionContext context)
    {
        var candidate = Path.GetFullPath(requested ?? context.WorkingDirectory, context.WorkingDirectory);
        var allowed = context.AuthorizedRoots.Append(context.WorkingDirectory)
            .Select(Path.GetFullPath)
            .Any(root => candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        return allowed
            ? candidate
            : throw new UnauthorizedAccessException("命令工作目录不在授权目录中。");
    }
}
