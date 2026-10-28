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
        bool acceptExitCodeOne = false,
        string? standardInput = null,
        BoundedTextBuffer? standardOutput = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? extraReadOnlyPaths = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = new BoundedTextBuffer(context.MaximumOutputCharacters);
        // When the caller supplies variables the ambient GIT_* isolation is kept by explicitly
        // nulling every ambient GIT_* name instead of the launcher prefix removal, so the
        // tool-provided values (e.g. GIT_TERMINAL_PROMPT) survive while ambient ones cannot.
        var overrides = environment is null ? null : IsolateGitEnvironment(environment);
        var isolation = context.CreateProcessIsolation();
        if (extraReadOnlyPaths is { Count: > 0 } readOnlyPaths)
            isolation = isolation with
            {
                ReadOnlyPaths = isolation.ReadOnlyPaths.Concat(readOnlyPaths).ToArray()
            };
        await using var process = processes.Start(new TaskProcessStartRequest("git", repository, arguments,
            RedirectStandardInput: standardInput is not null, Environment: overrides,
            RemovedEnvironmentPrefixes: overrides is null ? ["GIT_"] : null,
            Isolation: isolation), cancellationToken);
        if (standardInput is not null && process.StandardInput is null)
            throw new InvalidOperationException("Process launcher did not provide a standard input stream.");
        var stdout = PumpAsync(process.StandardOutput, output, cancellationToken, standardOutput);
        var stderr = PumpAsync(process.StandardError, output, cancellationToken);
        var input = standardInput is null ? null : WriteStandardInputAsync(process.StandardInput!, standardInput, cancellationToken);
        var readers = Task.WhenAll(input is null ? [(Task)stdout, stderr] : [(Task)stdout, stderr, input]);
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

    private static Dictionary<string, string?> IsolateGitEnvironment(IReadOnlyDictionary<string, string?> environment)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
            {
                merged[name] = null;
            }
        }

        foreach (var entry in environment) merged[entry.Key] = entry.Value;
        return merged;
    }

    private static async Task WriteStandardInputAsync(TextWriter writer, string input, CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteAsync(input.AsMemory(), cancellationToken);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            try { writer.Dispose(); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }

    private static async Task<bool> PumpAsync(
        TextReader reader,
        BoundedTextBuffer output,
        CancellationToken cancellationToken,
        BoundedTextBuffer? standardOutput = null)
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
            standardOutput?.Append(buffer.AsSpan(0, read));
            hasOutput = true;
        }
    }
}
