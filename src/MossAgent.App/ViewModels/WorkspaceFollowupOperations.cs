namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceFollowupOperations : IDisposable
{
    private readonly Lock _gate = new();
    private readonly SettingsOperationLifetime _lifetime = new();
    private readonly Dictionary<Guid, SemaphoreSlim> _taskGates = [];
    private readonly Dictionary<Guid, HashSet<CancellationTokenSource>> _preparing = [];
    private readonly HashSet<Task> _pending = [];
    private bool _stopping;

    public event Func<Task>? StateChanged;
    public bool IsPreparing(Guid taskId) { lock (_gate) return _preparing.ContainsKey(taskId); }
    public bool IsStopping { get { lock (_gate) return _stopping; } }

    public void CancelPreparation(Guid taskId)
    {
        CancellationTokenSource[] cancellations;
        lock (_gate) cancellations = _preparing.TryGetValue(taskId, out var entries) ? entries.ToArray() : [];
        foreach (var cancellation in cancellations)
        {
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    public Task RunAsync(Guid taskId, bool preparing, Func<CancellationToken, Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SemaphoreSlim taskGate;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            _pending.Add(completion.Task);
            if (!_taskGates.TryGetValue(taskId, out taskGate!)) _taskGates[taskId] = taskGate = new(1, 1);
        }
        _ = ExecuteAsync(taskId, taskGate, preparing, action, completion);
        return completion.Task;
    }

    private async Task ExecuteAsync(Guid taskId, SemaphoreSlim taskGate, bool preparing,
        Func<CancellationToken, Task> action, TaskCompletionSource completion)
    {
        Exception? failure = null;
        var acquired = false;
        CancellationTokenSource? registered = null;
        try
        {
            using var operation = _lifetime.CreateOperation(_lifetime.Token);
            if (preparing)
            {
                lock (_gate)
                {
                    if (!_preparing.TryGetValue(taskId, out var entries)) _preparing[taskId] = entries = [];
                    entries.Add(operation);
                }
                registered = operation;
                await NotifyAsync();
            }
            await taskGate.WaitAsync(operation.Token);
            acquired = true;
            await action(operation.Token);
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            if (registered is not null)
            {
                lock (_gate)
                {
                    if (_preparing.TryGetValue(taskId, out var entries))
                    {
                        entries.Remove(registered);
                        if (entries.Count == 0) _preparing.Remove(taskId);
                    }
                }
            }
            if (acquired) taskGate.Release();
            try { if (registered is not null) await NotifyAsync(); }
            catch (Exception exception) { failure ??= exception; }
            lock (_gate) _pending.Remove(completion.Task);
            if (failure is null) completion.TrySetResult();
            else { completion.TrySetException(failure); _ = completion.Task.Exception; }
        }
    }

    private async Task NotifyAsync()
    {
        foreach (var callback in StateChanged?.GetInvocationList().Cast<Func<Task>>() ?? []) await callback();
    }

    public async Task StopAsync()
    {
        Task[] pending;
        lock (_gate) { _stopping = true; pending = _pending.ToArray(); }
        _lifetime.Dispose();
        try { await Task.WhenAll(pending); }
        catch (OperationCanceledException) { }
    }

    public void Dispose() { lock (_gate) _stopping = true; _lifetime.Dispose(); }
}
