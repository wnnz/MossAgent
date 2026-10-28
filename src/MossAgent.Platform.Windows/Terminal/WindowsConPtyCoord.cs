using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Terminal;

/// <summary>COORD structure consumed by the ConPTY APIs.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowsConPtyCoord
{
    public short X;
    public short Y;
}
