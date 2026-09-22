using MossAgent.Application.Artifacts;

namespace MossAgent.Infrastructure.Persistence;

public sealed class TaskArtifactPaths(AppDataPaths paths) : ITaskArtifactPaths
{
    public string GetTaskDirectory(Guid taskId) =>
        Path.Combine(paths.ArtifactsPath, taskId.ToString("N"));
}

