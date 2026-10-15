using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsShellSelection
{
    public static void Reveal(string path, Action validatePath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException();
        Marshal.ThrowExceptionForHR(WindowsShellSelectionNative.CoInitializeEx(0, 2));
        nint folder = 0;
        nint file = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Marshal.ThrowExceptionForHR(WindowsShellSelectionNative.SHParseDisplayName(directory, 0, out folder, 0, out _));
            Marshal.ThrowExceptionForHR(WindowsShellSelectionNative.SHParseDisplayName(path, 0, out file, 0, out _));
            var child = WindowsShellSelectionNative.ILFindLastID(file);
            if (child == 0) throw new InvalidOperationException();
            validatePath();
            cancellationToken.ThrowIfCancellationRequested();
            Marshal.ThrowExceptionForHR(WindowsShellSelectionNative.SHOpenFolderAndSelectItems(folder, 1, [child], 0));
        }
        finally
        {
            if (file != 0) Marshal.FreeCoTaskMem(file);
            if (folder != 0) Marshal.FreeCoTaskMem(folder);
            WindowsShellSelectionNative.CoUninitialize();
        }
    }
}
