using System.Text;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Infrastructure.Git;

public sealed class GitProcessRunner(ITaskProcessLauncher processes, TimeProvider? timeProvider = null)
{
    private const int MaximumStreamCharacters = 16 * 1024;

    public async Task<GitProcessResult> RunAsync(string directory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await using var process = processes.Start(new TaskProcessStartRequest("git", directory, arguments, RemovedEnvironmentPrefixes: ["GIT_"]), cancellation.Token);
            var stdout = ReadAsync(process.StandardOutput, cancellation.Token);
            var stderr = ReadAsync(process.StandardError, cancellation.Token);
            var readers = Task.WhenAll((Task)stdout, stderr);
            try { await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false); }
            finally
            {
                try { await process.TerminateAsync().ConfigureAwait(false); }
                finally { await readers.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
            }
            await readers.ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            return new(process.ExitCode, stdout.Result.Text, stderr.Result.Text, stdout.Result.Truncated || stderr.Result.Truncated);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        catch (OperationCanceledException exception) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Git operation timed out.", exception);
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadAsync(TextReader reader, CancellationToken token)
    {
        var output = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            var accepted = Math.Min(count, MaximumStreamCharacters - output.Length);
            output.Append(buffer.AsSpan(0, accepted));
            truncated |= accepted < count;
        }
        return (output.ToString(), truncated);
    }
}
