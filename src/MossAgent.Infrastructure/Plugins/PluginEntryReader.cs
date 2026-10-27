using System.IO.Compression;
using MossAgent.Application.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Reads a single archive entry into memory with a hard byte limit. Oversized
/// content is rejected, never truncated; decompression failures are unreadable packages.</summary>
internal static class PluginEntryReader
{
    public static async Task<byte[]> ReadAllAsync(PluginArchiveEntry entry, int limit, string code, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (entry.Length > limit)
            throw new PluginPackageException(code, $"entry '{entry.NormalizedName}' exceeds its {limit}-byte manifest limit");
        try
        {
            await using var stream = entry.Entry.Open();
            var buffer = new byte[limit + 1];
            var total = 0;
            while (total <= limit)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total, limit + 1 - total), token);
                if (read == 0) break;
                total += read;
            }
            if (total > limit)
                throw new PluginPackageException(code, $"entry '{entry.NormalizedName}' exceeds its {limit}-byte manifest limit");
            return buffer.AsSpan(0, total).ToArray();
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            throw new PluginPackageException(PluginPackageErrorCode.Unreadable,
                $"entry '{entry.NormalizedName}' could not be decompressed", error);
        }
    }
}
