using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser.Artifacts;

/// <summary>
/// Chunked stream copy that refuses to write past the artifact limit. This is the
/// hard bound for downloads whose SDK temp path is unavailable (or resolves only at
/// completion): the destination never receives more than <paramref name="maximumBytes"/>
/// even when the source keeps producing bytes. A source larger than the limit leaves
/// a partial write that the owning artifact session discards on failure.
/// </summary>
internal static class BoundedBrowserArtifactCopy
{
    private const int ChunkSize = 64 * 1024;

    public static async Task CopyAsync(Stream source, Stream destination, long maximumBytes,
        CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        var buffer = new byte[ChunkSize];
        long written = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) return;
            var allowed = maximumBytes - written;
            if (read > allowed)
            {
                if (allowed > 0)
                    await destination.WriteAsync(buffer.AsMemory(0, (int)allowed), token).ConfigureAwait(false);
                throw new FileMutationSessionException(
                    BrowserToolText.Get("ArtifactTooLarge"), "browser_artifact_too_large");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            written += read;
        }
    }
}
