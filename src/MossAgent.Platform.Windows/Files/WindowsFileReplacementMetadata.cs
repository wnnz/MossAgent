using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsFileReplacementMetadata
{
    public static void Preserve(SafeFileHandle source, SafeFileHandle destination)
    {
        var attributes = Validate(source);
        WindowsFileSecurityCopy.Copy(source, destination);
        File.SetCreationTimeUtc(destination, File.GetCreationTimeUtc(source));
        File.SetAttributes(destination, attributes);
    }

    public static FileAttributes Validate(SafeFileHandle source)
    {
        var attributes = File.GetAttributes(source);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        if ((attributes & (FileAttributes.Encrypted | FileAttributes.SparseFile | FileAttributes.ReadOnly)) != 0)
            throw new FileWriteSessionException(WindowsFileText.WriteMetadataDenied, "file_write_metadata_unsupported");
        ValidateStreams(source);
        return attributes;
    }

    internal static void ValidateStreams(SafeFileHandle source)
    {
        // A rename would otherwise discard the target's additional named streams.
        var buffer = Marshal.AllocHGlobal(65536);
        try
        {
            if (NtQueryInformationFile(source, out _, buffer, 4, 7) < 0 || Marshal.ReadInt32(buffer) != 0
                || NtQueryInformationFile(source, out _, buffer, 65536, 22) < 0
                || Marshal.ReadInt32(buffer) != 0 || Marshal.ReadInt32(buffer, 4) != 14
                || Marshal.PtrToStringUni(buffer + 24, 7) != "::$DATA")
                throw new FileWriteSessionException(WindowsFileText.WriteMetadataDenied, "file_write_metadata_unsupported");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationFile(SafeFileHandle handle,
        out WindowsFileIoStatus status, IntPtr information, uint length, uint informationClass);
}
