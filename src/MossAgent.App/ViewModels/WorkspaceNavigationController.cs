using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceNavigationController
{
    private readonly WorkspaceNavigationHistory _history = new();
    private readonly Func<(Guid? ProjectId, Guid? TaskId)> _selection;
    private readonly Func<bool> _canNavigate;
    private readonly Action<(Guid? ProjectId, Guid? TaskId)> _navigate;

    public WorkspaceNavigationController(Func<(Guid? ProjectId, Guid? TaskId)> selection,
        Func<bool> canNavigate, Action<(Guid? ProjectId, Guid? TaskId)> navigate)
    {
        _selection = selection;
        _canNavigate = canNavigate;
        _navigate = navigate;
        BackCommand = new RelayCommand(Back, () => _canNavigate() && _history.CanGoBack);
        ForwardCommand = new RelayCommand(Forward, () => _canNavigate() && _history.CanGoForward);
    }

    public IRelayCommand BackCommand { get; }
    public IRelayCommand ForwardCommand { get; }

    public void Record((Guid? ProjectId, Guid? TaskId) target)
    {
        _history.Navigate(_selection(), target);
        NotifyAvailabilityChanged();
    }

    public void RemoveTask(Guid taskId)
    {
        _history.RemoveTask(taskId);
        NotifyAvailabilityChanged();
    }

    public void NotifyAvailabilityChanged()
    {
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
    }

    private void Back()
    {
        if (!BackCommand.CanExecute(null) || !_history.TryGoBack(_selection(), out var target)) return;
        NotifyAvailabilityChanged();
        _navigate(target);
    }

    private void Forward()
    {
        if (!ForwardCommand.CanExecute(null) || !_history.TryGoForward(_selection(), out var target)) return;
        NotifyAvailabilityChanged();
        _navigate(target);
    }
}
