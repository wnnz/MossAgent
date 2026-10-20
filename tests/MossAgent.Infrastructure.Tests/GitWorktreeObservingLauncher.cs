using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Infrastructure.Tests;

internal sealed class GitWorktreeObservingLauncher(ITaskProcessLauncher inner) : ITaskProcessLauncher
{
    public List<TaskProcessStartRequest> Requests { get; } = [];
    public Func<TaskProcessStartRequest, Task>? AfterExit { get; init; }

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return new GitWorktreeObservedProcess(inner.Start(request, cancellationToken),
            () => AfterExit?.Invoke(request) ?? Task.CompletedTask);
    }
}
