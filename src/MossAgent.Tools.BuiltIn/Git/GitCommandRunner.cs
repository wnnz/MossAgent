using System.Diagnostics;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Processes;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommandRunner
{
    public async Task<ToolResult> RunAsync(
        string repository,
        IReadOnlyList<string> arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var output = new BoundedTextBuffer(context.MaximumOutputCharacters);
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = PumpAsync(process.StandardOutput, output, cancellationToken);
        var stderr = PumpAsync(process.StandardError, output, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
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
            ? ToolResult.Success("Git 命令执行完成。", content)
            : ToolResult.Failure($"Git 命令失败，退出码 {process.ExitCode}。\n{content}", "git_failed");
    }

    private static async Task PumpAsync(
        TextReader reader,
        BoundedTextBuffer output,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            output.Append(buffer.AsSpan(0, read));
        }
    }
}
