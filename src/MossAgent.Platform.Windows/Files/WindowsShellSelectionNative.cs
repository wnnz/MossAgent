using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsShellSelectionNative
{
    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int SHParseDisplayName(string name, nint bindingContext, out nint item, uint attributes, out uint actualAttributes);
    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern nint ILFindLastID(nint item);
    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern int SHOpenFolderAndSelectItems(nint folder, uint count, [In] nint[] children, uint flags);
}
