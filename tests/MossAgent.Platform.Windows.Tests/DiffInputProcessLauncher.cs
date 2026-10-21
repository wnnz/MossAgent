using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class DiffInputProcessLauncher : ITaskProcessLauncher
{
    public int Starts { get; private set; }
    public Action<TaskProcessStartRequest>? Starting { get; set; }
    public DiffInputHeldProcess? Held { get; init; }
    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        Starts++;
        Starting?.Invoke(request);
        return Held ?? new FixtureTaskProcessLauncher().Start(request, token);
    }
}
