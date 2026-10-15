using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class AppShutdownViewModel : ObservableObject
{
    private static readonly ResourceManager Labels = new("MossAgent.App.Resources.ShutdownStrings", typeof(AppShutdownViewModel).Assembly);
    private bool _isPending;
    private bool _hasError;
    private Task? _cleanup;

    public AppShutdownViewModel()
    {
        ForceExitCommand = new RelayCommand(
            () => { if (HasError) ForceExitRequested?.Invoke(); }, () => HasError);
        RetryExitCommand = new RelayCommand(
            () => { if (HasError) RetryExitRequested?.Invoke(); }, () => HasError);
    }

    public event Action? ForceExitRequested;
    public event Action? RetryExitRequested;
    public IRelayCommand ForceExitCommand { get; }
    public IRelayCommand RetryExitCommand { get; }
    public bool IsPending => _isPending;
    public bool HasError => _hasError;
    public bool IsVisible => IsPending || HasError;
    public string Title => Label(HasError ? "FailedTitle" : "PendingTitle");
    public string Message => Label(HasError ? "FailedMessage" : "PendingMessage");
    public string ForceExitLabel => Label(nameof(ForceExitLabel));
    public string RetryExitLabel => Label(nameof(RetryExitLabel));

    public Task RunCleanupAsync(Func<Task> cleanup)
    {
        if (_cleanup is null || _cleanup.IsFaulted || _cleanup.IsCanceled) _cleanup = cleanup();
        return _cleanup;
    }

    public void Begin() => SetState(true, false);
    public void Complete() => SetState(false, false);
    public void Fail() => SetState(false, true);

    private void SetState(bool pending, bool failed)
    {
        _isPending = pending;
        _hasError = failed;
        foreach (var property in new[] { nameof(IsPending), nameof(HasError), nameof(IsVisible), nameof(Title), nameof(Message) })
            OnPropertyChanged(property);
        ForceExitCommand.NotifyCanExecuteChanged();
        RetryExitCommand.NotifyCanExecuteChanged();
    }

    private static string Label(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException($"Missing shutdown resource: {key}");
}
