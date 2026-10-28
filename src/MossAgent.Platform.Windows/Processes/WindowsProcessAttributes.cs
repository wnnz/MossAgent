using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsProcessAttributes : IDisposable
{
    private readonly List<nint> _values = [];
    private bool _initialized;
    public nint Handle { get; private set; }

    public WindowsProcessAttributes(SafeFileHandle job, nint securityCapabilities = 0, params nint[] inheritedHandles)
    {
        var attributeCount = securityCapabilities == 0 ? 2 : 3;
        nuint size = 0;
        _ = WindowsProcessNative.InitializeProcThreadAttributeList(0, attributeCount, 0, ref size);
        try
        {
            Handle = Marshal.AllocHGlobal(checked((nint)size));
            WindowsProcessNative.Require(WindowsProcessNative.InitializeProcThreadAttributeList(Handle, attributeCount, 0, ref size));
            _initialized = true;
            Add(WindowsProcessNative.ProcessAttributeHandleList, inheritedHandles);
            Add(WindowsProcessNative.ProcessAttributeJobList, [job.DangerousGetHandle()]);
            if (securityCapabilities != 0)
                AddValue(Sandbox.WindowsAppContainerNative.ProcessAttributeSecurityCapabilities,
                    securityCapabilities, (nuint)Marshal.SizeOf<Sandbox.WindowsSecurityCapabilities>());
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

    private void AddValue(nuint attribute, nint value, nuint size) =>
        WindowsProcessNative.Require(WindowsProcessNative.UpdateProcThreadAttribute(Handle, 0,
            attribute, value, size, 0, 0));

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
