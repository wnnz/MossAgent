using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Tools.Tests;

internal sealed class RejectingTaskProcessLauncher : ITaskProcessLauncher
{
    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A denied request must not launch a process.");
}
