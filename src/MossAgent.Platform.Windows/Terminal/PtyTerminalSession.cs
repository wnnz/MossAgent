using System.Text;
using Porta.Pty;

namespace MossAgent.Platform.Windows.Terminal;

internal sealed class PtyTerminalSession : IAsyncDisposable
{
    private readonly IPtyConnection _connection;
    private readonly CancellationTokenSource _shutdown;
    private readonly CancellationTokenSource _readShutdown = new();
    private readonly CancellationToken _writeToken;
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly object _sync = new();
    private readonly Task _readLoop;
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _closing;

    internal PtyTerminalSession(
        IPtyConnection connection,
        CancellationTokenSource shutdown,
        Action<string> output)
    {
        _connection = connection;
        _shutdown = shutdown;
        _writeToken = shutdown.Token;
        _connection.ProcessExited += OnProcessExited;
        _readLoop = ReadLoopAsync(connection.ReaderStream, output, _readShutdown.Token);
        Termination = ObserveTerminationAsync();
        try { if (connection.WaitForExit(0)) _exit.TrySetResult(connection.ExitCode); }
        catch (Exception exception) { _exit.TrySetException(exception); }
    }

    public Task<int> Termination { get; }
    private void OnProcessExited(object? sender, PtyExitedEventArgs eventArgs) => _exit.TrySetResult(eventArgs.ExitCode);

    private async Task<int> ObserveTerminationAsync()
    {
        var exitCode = await _exit.Task.ConfigureAwait(false);
        await _readLoop.ConfigureAwait(false);
        return exitCode;
    }

    public static Task<PtyTerminalSession> StartAsync(string workingDirectory, Action<string> output, CancellationToken cancellationToken) =>
        StartAsync(workingDirectory, output, cancellationToken, PtyProvider.SpawnAsync);

    internal static async Task<PtyTerminalSession> StartAsync(string workingDirectory, Action<string> output,
        CancellationToken cancellationToken, Func<PtyOptions, CancellationToken, Task<IPtyConnection>> spawn)
    {
        var options = new PtyOptions
        {
            Name = "MossAgent",
            Cols = 120,
            Rows = 30,
            Cwd = workingDirectory,
            App = "pwsh.exe",
            CommandLine = ["-NoLogo", "-NoProfile", "-NoExit", "-Command",
                "[Console]::InputEncoding = [Text.UTF8Encoding]::new($false); [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); $OutputEncoding = [Text.UTF8Encoding]::new($false)"],
            UseAsyncIo = true,
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await spawn(options, cancellationToken).ConfigureAwait(false);
        var session = new PtyTerminalSession(connection, new CancellationTokenSource(), output);
        if (cancellationToken.IsCancellationRequested)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return session;
    }

    public async Task WriteAsync(string input, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _writeToken);
        await _writes.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var bytes = Encoding.UTF8.GetBytes(input);
            await _connection.WriterStream.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
            await _connection.WriterStream.FlushAsync(linked.Token).ConfigureAwait(false);
        }
        finally { _writes.Release(); }
    }

    public void Resize(int columns, int rows) => _connection.Resize(columns, rows);

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_closing is not null) return new ValueTask(_closing);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _closing = completion.Task;
        }
        _ = CompleteCloseAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task CompleteCloseAsync(TaskCompletionSource completion)
    {
        try { await CloseAsync().ConfigureAwait(false); completion.SetResult(); }
        catch (Exception exception) { completion.SetException(exception); }
    }

    private async Task CloseAsync()
    {
        _connection.ProcessExited -= OnProcessExited;
        _exit.TrySetCanceled();
        try { _shutdown.Cancel(); }
        catch (AggregateException) { }
        try { await Task.Run(_connection.Dispose, CancellationToken.None).ConfigureAwait(false); }
        finally
        {
            _readShutdown.Cancel();
            await _readLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await ((Task)Termination).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await _writes.WaitAsync().ConfigureAwait(false);
            _writes.Release();
            _writes.Dispose();
            _readShutdown.Dispose();
            _shutdown.Dispose();
        }
    }

    private async Task ReadLoopAsync(
        Stream reader,
        Action<string> output,
        CancellationToken cancellationToken)
    {
        try
        {
            using var text = new StreamReader(reader, Encoding.UTF8, false, 8192, leaveOpen: true);
            var buffer = new char[4096];
            while (true)
            {
                var read = await text.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                output(new string(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) { _exit.TrySetException(exception); }
    }
}
