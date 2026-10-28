using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>SID_AND_ATTRIBUTES used for AppContainer capability SIDs.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowsSidAndAttributes
{
    public nint Sid;
    public uint Attributes;
}
