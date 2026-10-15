using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public class GuardedAsyncRelayCommand : IAsyncRelayCommand, IDisposable
{
    private readonly IAsyncRelayCommand _inner;
    private bool _disposed;

    public GuardedAsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool> canExecute)
        : this(new AsyncRelayCommand(execute, canExecute)) { }

    private protected GuardedAsyncRelayCommand(IAsyncRelayCommand inner)
    {
        _inner = inner;
        _inner.PropertyChanged += ForwardPropertyChanged;
        _inner.CanExecuteChanged += ForwardCanExecuteChanged;
    }

    private protected bool IsDisposed => _disposed;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CanExecuteChanged;
    public Task? ExecutionTask => _inner.ExecutionTask;
    public bool CanBeCanceled => !_disposed && _inner.CanBeCanceled;
    public bool IsCancellationRequested => _inner.IsCancellationRequested;
    public bool IsRunning => _inner.IsRunning;
    public bool CanExecute(object? parameter) => !_disposed && _inner.CanExecute(parameter);

    public Task ExecuteAsync(object? parameter) => CanExecute(parameter)
        ? _inner.ExecuteAsync(parameter) : Task.CompletedTask;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) _inner.Execute(parameter);
    }

    public void Cancel() => _inner.Cancel();
    public void NotifyCanExecuteChanged() => _inner.NotifyCanExecuteChanged();
    private void ForwardPropertyChanged(object? sender, PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
    private void ForwardCanExecuteChanged(object? sender, EventArgs args) => CanExecuteChanged?.Invoke(this, args);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inner.Cancel();
        _inner.PropertyChanged -= ForwardPropertyChanged;
        _inner.CanExecuteChanged -= ForwardCanExecuteChanged;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
