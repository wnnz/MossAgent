using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Testing;

internal sealed class RecordingTaskProcessLauncher(Func<ITaskProcess> create) : ITaskProcessLauncher
{
    public List<TaskProcessStartRequest> Requests { get; } = [];
    public List<CancellationToken> Tokens { get; } = [];

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        Requests.Add(request);
        Tokens.Add(token);
        return create();
    }
}
