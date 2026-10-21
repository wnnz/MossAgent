using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsRelativeFile
{
    internal const uint ReadAccess = 0x00120089; // Read data/attributes/control + synchronize.
    internal const uint DirectoryAccess = 0x001000a1; // List/traverse/attributes + synchronize.
    internal const uint StagingAccess = 0xc01f0000; // Read/write/delete + security + synchronize.
    internal const uint TemporaryAccess = 0xc0110000; // Generic read/write + delete + explicit synchronize for NT synchronous I/O.

    public static unsafe SafeFileHandle? Open(
        SafeFileHandle parent, string name, uint access, FileShare share, uint disposition,
        bool? directory, bool allowMissing = false, bool deleteOnClose = false) =>
        Open(parent, name, access, share, disposition, directory, out _, allowMissing, deleteOnClose);

    public static unsafe SafeFileHandle? Open(
        SafeFileHandle parent, string name, uint access, FileShare share, uint disposition,
        bool? directory, out bool created, bool allowMissing = false, bool deleteOnClose = false)
    {
        created = false;
        var added = false;
        try
        {
            WindowsWriteDirectoryCasePolicy.Validate(parent);
            parent.DangerousAddRef(ref added);
            fixed (char* characters = name)
            {
                var unicode = new WindowsFileUnicodeString
                {
                    Length = checked((ushort)(name.Length * 2)),
                    MaximumLength = checked((ushort)(name.Length * 2)), Buffer = (IntPtr)characters
                };
                var attributes = new WindowsFileObjectAttributes
                {
                    Length = (uint)Marshal.SizeOf<WindowsFileObjectAttributes>(),
                    RootDirectory = parent.DangerousGetHandle(), ObjectName = (IntPtr)(&unicode),
                    Attributes = 0x1040 // OBJ_DONT_REPARSE | OBJ_CASE_INSENSITIVE, on validated ordinary directories.
                };
                var options = 0x00200020u | (directory == true ? 1u : directory == false ? 0x40u : 0u)
                    | (deleteOnClose ? 0x1000u : 0u); // FILE_DELETE_ON_CLOSE belongs to this opened file object.
                var status = NtCreateFile(out var handle, access, ref attributes, out var io,
                    IntPtr.Zero, 0x80, share, disposition, options, IntPtr.Zero, 0);
                if (status >= 0) { created = io.Information.ToUInt64() == 2; return handle; }
                handle?.Dispose();
                if (allowMissing && status is unchecked((int)0xc0000034) or unchecked((int)0xc000003a)) return null;
                throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
            }
        }
        finally { if (added) parent.DangerousRelease(); }
    }

    public static void Rename(SafeFileHandle source, SafeFileHandle parent, string name, bool overwrite)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name);
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var size = checked(lengthOffset + 4 + nameBytes.Length);
        var buffer = Marshal.AllocHGlobal(size);
        var added = false;
        try
        {
            parent.DangerousAddRef(ref added);
            Marshal.Copy(new byte[size], 0, buffer, size);
            // DELETE access respects existing readers' delete-sharing restrictions.
            // POSIX replacement lets our rename retain the verified read handle.
            Marshal.WriteInt32(buffer, overwrite ? 3 : 0);
            Marshal.WriteIntPtr(buffer, rootOffset, parent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, nameBytes.Length);
            Marshal.Copy(nameBytes, 0, buffer + lengthOffset + 4, nameBytes.Length);
            var status = NtSetInformationFile(source, out _, buffer, (uint)size, 65);
            if (status < 0) throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
        }
        finally
        {
            if (added) parent.DangerousRelease();
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static void DeleteOnClose(SafeFileHandle handle)
    {
        var buffer = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(buffer, 1);
            if (NtSetInformationFile(handle, out _, buffer, 1, 13) < 0)
                throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access,
        ref WindowsFileObjectAttributes attributes, out WindowsFileIoStatus status,
        IntPtr allocationSize, uint fileAttributes, FileShare share, uint disposition,
        uint options, IntPtr eaBuffer, uint eaLength);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtSetInformationFile(SafeFileHandle handle,
        out WindowsFileIoStatus status, IntPtr information, uint length, uint informationClass);
}
