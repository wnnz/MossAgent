using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Artifacts;

public interface ITaskArtifactContentReader
{
    Task<byte[]> ReadAsync(Guid taskId, ToolArtifact artifact, CancellationToken cancellationToken);
}
