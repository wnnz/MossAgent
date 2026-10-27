namespace MossAgent.Infrastructure.Plugins;

/// <summary>Incremental IEEE 802.3 CRC-32 over copied bytes, verified against the archive's
/// stored CRC per entry. The declared uncompressed size alone cannot be trusted: the runtime
/// truncates decompression at it, so only the checksum proves the payload is complete.</summary>
internal static class ArchiveCrc32
{
    private const uint Polynomial = 0xEDB88320;
    private const uint InitialValue = 0xFFFFFFFF;
    private static readonly uint[] Table = BuildTable();

    public static uint Start() => InitialValue;

    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var item in data)
            crc = (crc >> 8) ^ Table[(crc ^ item) & 0xFF];
        return crc;
    }

    public static uint Finish(uint crc) => ~crc;

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var crc = index;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            table[index] = crc;
        }
        return table;
    }
}
