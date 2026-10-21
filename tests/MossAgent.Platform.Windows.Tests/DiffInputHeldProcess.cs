using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class DiffInputHeldProcess : ITaskProcess
{
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Terminated { get; private set; }
    public Action? Stopping { get; set; }
    public int Id => 1;
    public int ExitCode => 0;
    public TextWriter? StandardInput => null;
    public TextReader StandardOutput { get; } = new StringReader("");
    public TextReader StandardError { get; } = new StringReader("");
    public async Task WaitForExitAsync(CancellationToken token)
    {
        Started.TrySetResult();
        await _stopped.Task.WaitAsync(token);
    }
    public Task TerminateAsync()
    {
        if (!Terminated) Stopping?.Invoke();
        Terminated = true;
        _stopped.TrySetResult();
        return Task.CompletedTask;
    }
    public async ValueTask DisposeAsync()
    {
        try { await TerminateAsync(); }
        finally { StandardOutput.Dispose(); StandardError.Dispose(); }
    }
}
