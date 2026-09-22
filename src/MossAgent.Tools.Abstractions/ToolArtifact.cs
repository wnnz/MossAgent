namespace MossAgent.Tools.Abstractions;

public sealed record ToolArtifact(
    string Name,
    string Path,
    string MediaType,
    long Length,
    string Sha256);

