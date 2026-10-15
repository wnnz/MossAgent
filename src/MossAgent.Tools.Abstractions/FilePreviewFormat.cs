using System.Buffers.Binary;
using System.Text;

namespace MossAgent.Tools.Abstractions;

public static class FilePreviewFormat
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public const int MaximumTextBytes = 128 * 1024;
    public const long MaximumPixels = 8_000_000;

    public static string Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            if (bytes.Length < 33 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
                throw new InvalidDataException();
            ValidateDimensions(BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)),
                BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)));
            return "image/png";
        }
        if (bytes.StartsWith(new byte[] { 255, 216 })) return DetectJpeg(bytes);
        if (bytes.Length > MaximumTextBytes || bytes.Contains((byte)0)) throw new InvalidDataException();
        _ = new UTF8Encoding(false, true).GetString(bytes);
        return "text/plain";
    }

    private static string DetectJpeg(ReadOnlySpan<byte> bytes)
    {
        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset++] != 255) throw new InvalidDataException();
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset + 3 > bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 216 or 217 or 218) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length) break;
            if (marker is 192 or 193 or 194)
            {
                if (length < 8) break;
                ValidateDimensions(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)));
                return "image/jpeg";
            }
            offset += length;
        }
        throw new InvalidDataException();
    }

    private static void ValidateDimensions(long width, long height)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || width * height > MaximumPixels)
            throw new InvalidDataException();
    }
}
