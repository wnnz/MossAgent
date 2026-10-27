namespace MossAgent.Infrastructure.Plugins;

/// <summary>Outcome of extracting one package: the whole-archive SHA-256, the per-file
/// SHA-256 of every file entry keyed by normalized archive name, and the normalized names
/// of the directory entries. Directory entries carry no payload so they hold no hash, but
/// they are still real archive content — a component backed by one is not a missing file.
/// File hashes are computed during the bounded write, never by re-reading the extracted
/// files.</summary>
internal sealed record PluginExtractionResult(string ContentHash,
    IReadOnlyDictionary<string, string> FileHashes, IReadOnlySet<string> Directories);
