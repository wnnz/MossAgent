using MossAgent.Domain;

namespace MossAgent.Application.Authorization;

public sealed class ProjectAuthorizationNotQuiescedException(
    ProjectProfile savedProject,
    int outstandingOperations)
    : Exception("Project authorization was saved before all previous operations exited.")
{
    public ProjectProfile SavedProject { get; } = savedProject;
    public int OutstandingOperations { get; } = outstandingOperations;
}
