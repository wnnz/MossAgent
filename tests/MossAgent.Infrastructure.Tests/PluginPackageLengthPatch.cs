using System.Text;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Rewrites the declared uncompressed-size field of one central-directory record so a
/// test can lie about an entry's length while leaving the actual compressed payload intact.
/// The declared value is metadata only; decompression still produces the real bytes.</summary>
internal static class PluginPackageLengthPatch
{
    private const uint EndOfCentralDirectorySignature = 0x06054b50;
    private const uint CentralDirectorySignature = 0x02014b50;
    private const int UncompressedSizeOffset = 24;
    private const int NameLengthOffset = 28;
    private const int ExtraLengthOffset = 30;
    private const int CommentLengthOffset = 32;
    private const int RecordHeaderBytes = 46;

    public static void SetDeclaredLength(string packagePath, string entryName, uint declaredBytes)
    {
        var bytes = File.ReadAllBytes(packagePath);
        var record = FindRecord(bytes, entryName);
        BitConverter.GetBytes(declaredBytes).CopyTo(bytes, record + UncompressedSizeOffset);
        File.WriteAllBytes(packagePath, bytes);
    }

    private static int FindRecord(byte[] bytes, string entryName)
    {
        var directory = CentralDirectoryOffset(bytes);
        while (BitConverter.ToUInt32(bytes, directory) == CentralDirectorySignature)
        {
            var nameLength = BitConverter.ToUInt16(bytes, directory + NameLengthOffset);
            var name = Encoding.UTF8.GetString(bytes, directory + RecordHeaderBytes, nameLength);
            if (name == entryName) return directory;
            directory += RecordHeaderBytes + nameLength
                + BitConverter.ToUInt16(bytes, directory + ExtraLengthOffset)
                + BitConverter.ToUInt16(bytes, directory + CommentLengthOffset);
        }
        throw new InvalidOperationException($"entry '{entryName}' was not found in the central directory");
    }

    private static int CentralDirectoryOffset(byte[] bytes)
    {
        var tail = bytes.Length - 22;
        if (tail < 0 || BitConverter.ToUInt32(bytes, tail) != EndOfCentralDirectorySignature)
            throw new InvalidDataException("the fixture zip has no end-of-central-directory record");
        return (int)BitConverter.ToUInt32(bytes, tail + 16);
    }
}
