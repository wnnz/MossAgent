namespace MossAgent.Tools.Abstractions;

public sealed record ToolArtifactRecord(
    Guid Id,
    Guid ExecutionId,
    Guid TaskId,
    string Name,
    string Path,
    string MediaType,
    long Length,
    string Sha256,
    DateTimeOffset CreatedAt);
