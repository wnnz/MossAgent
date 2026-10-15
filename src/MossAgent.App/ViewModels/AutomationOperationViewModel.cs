using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class AutomationOperationViewModel(TimeProvider clock) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _active;
    private string _messageKey = "SavingPlan";
    private bool _running;
    private bool _refreshing;
    private bool _disposed;
    private IRelayCommand? _cancelCommand;

    public IRelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel, () => CanCancel);
    public bool IsRunning { get => _running; private set { if (SetProperty(ref _running, value)) NotifyState(); } }
    public bool CanCancel => !_disposed && IsRunning && !_refreshing && _active is { IsCancellationRequested: false };
    public string Message => AutomationText.Get(_refreshing ? "RefreshingPlans"
        : _active?.IsCancellationRequested == true ? "CancellingOperation" : _messageKey);
    internal bool IsAlive => !_disposed;

    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken commandToken,
        string progressKey, Action<string> reportFailure, Func<Task>? refresh = null,
        Func<Exception, string>? classifyFailure = null)
    {
        if (_disposed || IsRunning || commandToken.IsCancellationRequested) return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, commandToken, deadline.Token);
        _active = operation;
        _messageKey = progressKey;
        using var registration = operation.Token.Register(() => _ = AvaloniaUiThread.RunAsync(NotifyState, CancellationToken.None));
        IsRunning = true;
        try
        {
            operation.Token.ThrowIfCancellationRequested();
            await action(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed) reportFailure(AutomationText.Get(deadline.IsCancellationRequested ? "OperationTimedOut" : "OperationCancelled"));
        }
        catch (Exception exception)
        {
            if (!_disposed) reportFailure(AutomationText.Get(classifyFailure?.Invoke(exception) ?? "SaveError"));
        }
        finally
        {
            try
            {
                if (refresh is not null && !_disposed)
                {
                    _refreshing = true;
                    NotifyState();
                    await refresh();
                }
            }
            catch (Exception) { if (!_disposed) reportFailure(AutomationText.Get("LoadError")); }
            finally { _refreshing = false; _active = null; IsRunning = false; }
        }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(CanCancel));
        _cancelCommand?.NotifyCanExecuteChanged();
    }

    private void Cancel() { if (CanCancel) _active!.Cancel(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        NotifyState();
    }
}
