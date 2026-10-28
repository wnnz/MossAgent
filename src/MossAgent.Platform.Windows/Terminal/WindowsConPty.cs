using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Platform.Windows.Sandbox;

namespace MossAgent.Platform.Windows.Terminal;

/// <summary>
/// A kernel pseudoconsole bound to two anonymous pipes. The PTY side of the
/// pipes is handed to <c>CreatePseudoConsole</c>; the client side streams are
/// exposed through <see cref="OpenReader"/>/<see cref="OpenWriter"/>. Closing
/// the pseudoconsole ends the conhost side and drains the client read stream.
/// </summary>
internal sealed class WindowsConPty : IDisposable
{
    private readonly nint _handle;
    private SafeFileHandle? _inputWrite;
    private SafeFileHandle? _outputRead;
    private int _disposed;

    private WindowsConPty(nint handle, SafeFileHandle inputWrite, SafeFileHandle outputRead)
    {
        _handle = handle;
        _inputWrite = inputWrite;
        _outputRead = outputRead;
    }

    public nint Handle => _handle;

    public static WindowsConPty Create(short columns, short rows)
    {
        // CreatePseudoConsole relays the PTY-side ends into the conhost child through
        // PROC_THREAD_ATTRIBUTE_HANDLE_LIST, so those two ends must be inheritable;
        // the client-side ends stay same-process and are never marked.
        WindowsProcessNative.Require(WindowsConPtyNative.CreatePipe(out var inputRead, out var inputWrite, 0, 0));
        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        try
        {
            WindowsProcessNative.Require(WindowsConPtyNative.CreatePipe(out outputRead, out outputWrite, 0, 0));
            WindowsProcessNative.Require(WindowsConPtyNative.SetHandleInformation(inputRead, 1, 1));
            WindowsProcessNative.Require(WindowsConPtyNative.SetHandleInformation(outputWrite, 1, 1));
            var size = new WindowsConPtyCoord { X = columns, Y = rows };
            var result = WindowsConPtyNative.CreatePseudoConsole(size,
                inputRead.DangerousGetHandle(), outputWrite.DangerousGetHandle(), 0, out var handle);
            WindowsAppContainerNative.RequireSuccess(result, "CreatePseudoConsole");
            return new WindowsConPty(handle, inputWrite, outputRead);
        }
        catch
        {
            inputWrite.Dispose();
            outputRead?.Dispose();
            throw;
        }
        finally
        {
            // The pseudoconsole holds its own copies of the PTY-side ends.
            inputRead.Dispose();
            outputWrite?.Dispose();
        }
    }

    public Stream OpenReader()
    {
        var handle = _outputRead ?? throw new ObjectDisposedException(nameof(WindowsConPty));
        _outputRead = null;
        // Anonymous CreatePipe handles are synchronous; ReadAsync/WriteAsync on a
        // sync FileStream still complete and pending I/O unblocks on Dispose.
        return new FileStream(handle, FileAccess.Read, 4096, isAsync: false);
    }

    public Stream OpenWriter()
    {
        var handle = _inputWrite ?? throw new ObjectDisposedException(nameof(WindowsConPty));
        _inputWrite = null;
        return new FileStream(handle, FileAccess.Write, 4096, isAsync: false);
    }

    public void Resize(int columns, int rows)
    {
        // After the child exits the monitor closes the console; a late resize
        // must be a no-op rather than fault the caller.
        if (_disposed != 0) return;
        WindowsAppContainerNative.RequireSuccess(WindowsConPtyNative.ResizePseudoConsole(_handle,
            new WindowsConPtyCoord { X = checked((short)columns), Y = checked((short)rows) }), "ResizePseudoConsole");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        WindowsConPtyNative.ClosePseudoConsole(_handle);
        _inputWrite?.Dispose();
        _outputRead?.Dispose();
    }
}
