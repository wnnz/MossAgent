using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.App.Tests;

internal sealed class StatisticsCompletionGate(bool failCurrent) : ITaskProcessLauncher
{
    private readonly WindowsTaskProcessLauncher _inner = new();
    private int _selected;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken)
    {
        var process = _inner.Start(request, cancellationToken);
        if (request.FileName != "git" || !request.Arguments.Contains("--numstat")) return process;
        var ordinal = Interlocked.Increment(ref _selected);
        return new StatisticsGatedProcess(process, this, ordinal == 1, failCurrent && ordinal > 1);
    }
}
