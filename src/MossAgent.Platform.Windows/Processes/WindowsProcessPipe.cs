using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsProcessPipe : IDisposable
{
    private PipeStream? _read;
    private PipeStream? _write;

    private WindowsProcessPipe(PipeStream read, PipeStream write) => (_read, _write) = (read, write);

    public nint ReadHandle => _read!.SafePipeHandle.DangerousGetHandle();
    public nint WriteHandle => _write!.SafePipeHandle.DangerousGetHandle();

    public static WindowsProcessPipe Create(bool childReads = false)
    {
        var name = "MossAgent.Process." + Guid.NewGuid().ToString("N");
        var parent = new NamedPipeServerStream(name, childReads ? PipeDirection.Out : PipeDirection.In,
            1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance,
            4096, 4096);
        NamedPipeClientStream? child = null;
        try
        {
            child = new NamedPipeClientStream(".", name, childReads ? PipeDirection.In : PipeDirection.Out,
                PipeOptions.None, TokenImpersonationLevel.Anonymous, HandleInheritability.Inheritable);
            child.Connect(1000);
            parent.WaitForConnection();
            return childReads ? new WindowsProcessPipe(child, parent) : new WindowsProcessPipe(parent, child);
        }
        catch { child?.Dispose(); parent.Dispose(); throw; }
    }

    public TextReader OpenReader()
    {
        var stream = _read!;
        _read = null;
        try { return new StreamReader(stream, Encoding.UTF8); }
        catch { stream.Dispose(); throw; }
    }

    public TextWriter OpenWriter()
    {
        var stream = _write!;
        _write = null;
        try
        {
            return new StreamWriter(stream,
                new UTF8Encoding(false))
            { AutoFlush = true };
        }
        catch { stream.Dispose(); throw; }
    }

    public void CloseWriter() => _write?.Dispose();

    public void Dispose()
    {
        _read?.Dispose();
        _write?.Dispose();
    }
}
