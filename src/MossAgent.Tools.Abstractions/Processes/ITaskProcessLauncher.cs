namespace MossAgent.Tools.Abstractions.Processes;

public interface ITaskProcessLauncher
{
    ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken);
}
