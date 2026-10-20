namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceLoadCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RunAsync(Func<Task> load)
    {
        await _gate.WaitAsync();
        try { await load(); }
        finally { _gate.Release(); }
    }
}
