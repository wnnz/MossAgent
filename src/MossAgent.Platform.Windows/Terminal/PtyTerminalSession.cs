using System.Text;
using Porta.Pty;

namespace MossAgent.Platform.Windows.Terminal;

internal sealed class PtyTerminalSession : IAsyncDisposable
{
    private readonly IPtyConnection _connection;
    private readonly CancellationTokenSource _shutdown;
    private readonly Task _readLoop;

    private PtyTerminalSession(
        IPtyConnection connection,
        CancellationTokenSource shutdown,
        Action<string> output)
    {
        _connection = connection;
        _shutdown = shutdown;
        _readLoop = ReadLoopAsync(connection.ReaderStream, output, shutdown.Token);
    }

    public static async Task<PtyTerminalSession> StartAsync(
        string workingDirectory,
        Action<string> output,
        CancellationToken cancellationToken)
    {
        var options = new PtyOptions
        {
            Name = "MossAgent",
            Cols = 120,
            Rows = 30,
            Cwd = workingDirectory,
            App = "pwsh.exe",
            CommandLine = ["-NoLogo"],
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" }
        };
        var connection = await PtyProvider.SpawnAsync(options, cancellationToken);
        return new PtyTerminalSession(connection, new CancellationTokenSource(), output);
    }

    public async Task WriteAsync(string input, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        await _connection.WriterStream.WriteAsync(bytes, cancellationToken);
        await _connection.WriterStream.FlushAsync(cancellationToken);
    }

    public void Resize(int columns, int rows) => _connection.Resize(columns, rows);

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _connection.Kill();
        await _readLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _connection.Dispose();
        _shutdown.Dispose();
    }

    private static async Task ReadLoopAsync(
        Stream reader,
        Action<string> output,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                output(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

