using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Platform.Windows.Sandbox;

namespace MossAgent.Platform.Windows.Terminal;

/// <summary>
/// Proc-thread attribute list for a pseudoconsole child: job assignment plus
/// the HPCON attachment, optionally extended with SECURITY_CAPABILITIES.
/// </summary>
internal sealed class WindowsConPtyAttributes : IDisposable
{
    private readonly List<nint> _values = [];
    private bool _initialized;
    public nint Handle { get; private set; }

    public WindowsConPtyAttributes(SafeFileHandle job, nint pseudoconsole, nint securityCapabilities = 0)
    {
        var attributeCount = securityCapabilities == 0 ? 2 : 3;
        nuint size = 0;
        _ = WindowsProcessNative.InitializeProcThreadAttributeList(0, attributeCount, 0, ref size);
        try
        {
            Handle = Marshal.AllocHGlobal(checked((nint)size));
            WindowsProcessNative.Require(WindowsProcessNative.InitializeProcThreadAttributeList(Handle, attributeCount, 0, ref size));
            _initialized = true;
            Add(WindowsProcessNative.ProcessAttributeJobList, [job.DangerousGetHandle()]);
            // lpValue is the HPCON itself: kernelbase dereferences it as a pointer
            // into the caller's PseudoConsole struct, not as a handle buffer.
            AddValue(WindowsConPtyNative.ProcessAttributePseudoConsole, pseudoconsole, (nuint)nint.Size);
            if (securityCapabilities != 0)
                AddValue(WindowsAppContainerNative.ProcessAttributeSecurityCapabilities,
                    securityCapabilities, (nuint)Marshal.SizeOf<WindowsSecurityCapabilities>());
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
