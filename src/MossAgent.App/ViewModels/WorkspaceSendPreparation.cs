using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSendPreparation(TimeProvider? timeProvider = null) : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private Task _execution = Task.CompletedTask;
    private bool _disposed;

    public event Action? StateChanged;
    public bool IsActive => _cancellation is not null;
    public bool IsCancellationRequested => _cancellation?.IsCancellationRequested == true;
    public bool CanCancel => !_disposed && _cancellation is { IsCancellationRequested: false };

    public Task Start(Func<CancellationToken, Task> prepare)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsActive) throw new InvalidOperationException("A send is already being prepared.");
        var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellation = cancellation;
        _execution = completion.Task;
        _ = RunAsync(prepare, cancellation, completion);
        return completion.Task;
    }

    public void Cancel()
    {
        if (_cancellation is not { IsCancellationRequested: false } cancellation) return;
        cancellation.Cancel();
        StateChanged?.Invoke();
    }

    public Task StopAsync()
    {
        var execution = _execution;
        Cancel();
        return execution;
    }

    public async Task<AgentTask> CancelPreparedTaskAsync(WorkspaceTaskFactory factory, AgentTask task)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        try { return await factory.SetStatusAsync(task, AgentTaskStatus.Cancelled, deadline.Token); }
        catch (Exception exception) { throw new WorkspacePreparationFinalizationException(task.Id, exception); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StateChanged = null;
        Cancel();
    }

    private async Task RunAsync(Func<CancellationToken, Task> prepare, CancellationTokenSource cancellation,
        TaskCompletionSource completion)
    {
        var token = cancellation.Token;
        Exception? failure = null;
        try
        {
            StateChanged?.Invoke();
            await prepare(token);
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            _cancellation = null;
            cancellation.Dispose();
            try { StateChanged?.Invoke(); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            if (failure is OperationCanceledException && token.IsCancellationRequested) completion.TrySetCanceled(token);
            else if (failure is not null) completion.TrySetException(failure);
            else completion.TrySetResult();
        }
    }
}
