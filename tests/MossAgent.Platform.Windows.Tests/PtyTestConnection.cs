using Porta.Pty;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class PtyTestConnection : IPtyConnection
{
    public event EventHandler<PtyExitedEventArgs>? ProcessExited;
    public Stream ReaderStream { get; init; } = new MemoryStream();
    public Stream WriterStream { get; init; } = new MemoryStream();
    public int Pid => 1;
    public int ExitCode { get; set; }
    public bool HasExited { get; set; }
    public int DisposeCount { get; private set; }
    public bool KillThrows { get; init; }
    public Action? Disposing { get; init; }
    public Action<int, int>? Resizing { get; init; }
    public bool WaitForExit(int milliseconds) => HasExited;
    public void Exit(int exitCode)
    {
        ExitCode = exitCode;
        HasExited = true;
        var args = (PtyExitedEventArgs)Activator.CreateInstance(typeof(PtyExitedEventArgs),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, [exitCode], null)!;
        ProcessExited?.Invoke(this, args);
    }
    public void Resize(int cols, int rows) => Resizing?.Invoke(cols, rows);
    public void Kill()
    {
        if (KillThrows) throw new InvalidOperationException("synthetic-exited-process");
    }
    public void Dispose()
    {
        DisposeCount++;
        Disposing?.Invoke();
        ReaderStream.Dispose();
        WriterStream.Dispose();
    }
}
