namespace MossAgent.Application.Artifacts;

public interface ITaskArtifactPaths
{
    string GetTaskDirectory(Guid taskId);
}
