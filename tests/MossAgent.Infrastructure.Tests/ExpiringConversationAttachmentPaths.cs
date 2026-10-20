using MossAgent.Application.Artifacts;

namespace MossAgent.Infrastructure.Tests;

internal sealed class ExpiringConversationAttachmentPaths(ITaskArtifactPaths paths, ManualToolTimeProvider clock) : ITaskArtifactPaths
{
    public string GetTaskDirectory(Guid taskId)
    {
        clock.Advance(TimeSpan.FromSeconds(30));
        return paths.GetTaskDirectory(taskId);
    }
}
