using MossAgent.Application.Artifacts;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class PreviewProbeArtifactReader : ITaskArtifactContentReader
{
    public Func<Guid, ToolArtifact, CancellationToken, Task<byte[]>> Read { get; set; } =
        (_, _, _) => Task.FromResult(System.Text.Encoding.UTF8.GetBytes("public class Preview { }"));
    public Task<byte[]> ReadAsync(Guid taskId, ToolArtifact artifact, CancellationToken cancellationToken) => Read(taskId, artifact, cancellationToken);
}
