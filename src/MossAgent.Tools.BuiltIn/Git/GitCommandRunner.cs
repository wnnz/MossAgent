using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Processes;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommandRunner(ITaskProcessLauncher processes)
{
    public async Task<ToolResult> RunAsync(
        string repository,
        IReadOnlyList<string> arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken,
        bool acceptExitCodeOne = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = new BoundedTextBuffer(context.MaximumOutputCharacters);
        await using var process = processes.Start(new TaskProcessStartRequest("git", repository, arguments, RemovedEnvironmentPrefixes: ["GIT_"]), cancellationToken);
        var stdout = PumpAsync(process.StandardOutput, output, cancellationToken);
        var stderr = PumpAsync(process.StandardError, output, cancellationToken);
        var readers = Task.WhenAll((Task)stdout, stderr);
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
        cancellationToken.ThrowIfCancellationRequested();

        var content = output.ToString();
        return process.ExitCode == 0 || acceptExitCodeOne && process.ExitCode == 1 && !stderr.Result
            ? ToolResult.Success("Git 命令执行完成。", content)
            : ToolResult.Failure($"Git 命令失败，退出码 {process.ExitCode}。\n{content}", "git_failed");
    }

    private static async Task<bool> PumpAsync(
        TextReader reader,
        BoundedTextBuffer output,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var hasOutput = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return hasOutput;
            }

            output.Append(buffer.AsSpan(0, read));
            hasOutput = true;
        }
    }
}
