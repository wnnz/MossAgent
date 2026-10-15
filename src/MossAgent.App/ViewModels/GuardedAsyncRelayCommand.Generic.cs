using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class GuardedAsyncRelayCommand<T> : GuardedAsyncRelayCommand, IAsyncRelayCommand<T>
{
    private readonly AsyncRelayCommand<T> _inner;

    public GuardedAsyncRelayCommand(Func<T?, CancellationToken, Task> execute, Predicate<T?> canExecute)
        : this(new AsyncRelayCommand<T>(execute, canExecute)) { }

    private GuardedAsyncRelayCommand(AsyncRelayCommand<T> inner) : base(inner) => _inner = inner;

    public bool CanExecute(T? parameter) => !IsDisposed && _inner.CanExecute(parameter);
    public Task ExecuteAsync(T? parameter) => CanExecute(parameter) ? _inner.ExecuteAsync(parameter) : Task.CompletedTask;

    public void Execute(T? parameter)
    {
        if (CanExecute(parameter)) _inner.Execute(parameter);
    }

}
