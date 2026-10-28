using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>SECURITY_CAPABILITIES passed via PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowsSecurityCapabilities
{
    public nint AppContainerSid;
    public nint Capabilities;
    public uint CapabilityCount;
    public uint Reserved;
}
