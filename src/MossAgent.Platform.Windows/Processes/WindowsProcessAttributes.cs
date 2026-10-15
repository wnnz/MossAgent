using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsProcessAttributes : IDisposable
{
    private readonly List<nint> _values = [];
    private bool _initialized;
    public nint Handle { get; private set; }

    public WindowsProcessAttributes(SafeFileHandle job, params nint[] inheritedHandles)
    {
        nuint size = 0;
        _ = WindowsProcessNative.InitializeProcThreadAttributeList(0, 2, 0, ref size);
        try
        {
            Handle = Marshal.AllocHGlobal(checked((nint)size));
            WindowsProcessNative.Require(WindowsProcessNative.InitializeProcThreadAttributeList(Handle, 2, 0, ref size));
            _initialized = true;
            Add(WindowsProcessNative.ProcessAttributeHandleList, inheritedHandles);
            Add(WindowsProcessNative.ProcessAttributeJobList, [job.DangerousGetHandle()]);
        }
        catch { Dispose(); throw; }
    }

    private void Add(nuint attribute, nint[] handles)
    {
        var size = checked(handles.Length * nint.Size);
        var value = Marshal.AllocHGlobal(size);
        _values.Add(value);
        Marshal.Copy(handles, 0, value, handles.Length);
        WindowsProcessNative.Require(WindowsProcessNative.UpdateProcThreadAttribute(Handle, 0,
            attribute, value, (nuint)size, 0, 0));
    }

    public void Dispose()
    {
        if (_initialized) WindowsProcessNative.DeleteProcThreadAttributeList(Handle);
        foreach (var value in _values) Marshal.FreeHGlobal(value);
        if (Handle != 0) Marshal.FreeHGlobal(Handle);
        _values.Clear();
        _initialized = false;
        Handle = 0;
    }
}
