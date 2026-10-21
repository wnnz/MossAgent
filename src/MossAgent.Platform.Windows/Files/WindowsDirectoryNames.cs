using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsDirectoryNames
{
    public static IEnumerable<string> Read(SafeFileHandle directory, CancellationToken token)
    {
        const int size = 65536;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var restart = true;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var status = NtQueryDirectoryFile(directory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    out var io, buffer, size, 12, false, IntPtr.Zero, restart);
                restart = false;
                if (status == unchecked((int)0x80000006)) yield break; // STATUS_NO_MORE_FILES
                if (status < 0 || io.Information.ToUInt64() is 0 or > size)
                    throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
                foreach (var name in Parse(buffer, checked((int)io.Information.ToUInt64())))
                {
                    token.ThrowIfCancellationRequested();
                    if (name is not "." and not "..") yield return name;
                }
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IEnumerable<string> Parse(IntPtr buffer, int length)
    {
        var offset = 0;
        while (true)
        {
            if (offset > length - 12) throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
            var next = Marshal.ReadInt32(buffer, offset);
            var nameLength = Marshal.ReadInt32(buffer, offset + 8);
            if (nameLength <= 0 || nameLength % 2 != 0 || nameLength > length - offset - 12)
                throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
            yield return Marshal.PtrToStringUni(buffer + offset + 12, nameLength / 2)!;
            if (next == 0) yield break;
            if (next % 4 != 0 || next < 12 + nameLength || next > length - offset - 12)
                throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
            offset += next;
        }
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryDirectoryFile(SafeFileHandle handle, IntPtr @event,
        IntPtr apcRoutine, IntPtr apcContext, out WindowsFileIoStatus io,
        IntPtr information, uint length, uint informationClass,
        [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry, IntPtr fileName,
        [MarshalAs(UnmanagedType.U1)] bool restartScan);
}
