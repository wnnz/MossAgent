using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsStartupInfo
{
    public uint Size;
    public nint Reserved;
    public nint Desktop;
    public nint Title;
    public uint X;
    public uint Y;
    public uint Width;
    public uint Height;
    public uint XCharacterCount;
    public uint YCharacterCount;
    public uint FillAttribute;
    public uint Flags;
    public ushort ShowWindow;
    public ushort ReservedSize;
    public nint ReservedPointer;
    public nint StandardInput;
    public nint StandardOutput;
    public nint StandardError;
}
