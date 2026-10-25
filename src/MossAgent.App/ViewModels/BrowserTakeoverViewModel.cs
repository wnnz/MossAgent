using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

public sealed class BrowserTakeoverViewModel : ObservableObject, IDisposable
{
    private readonly IBrowserTakeoverController _controller;
    private readonly EmbeddedBrowserWorkspaceViewModel? _embeddedBrowsers;
    private Guid? _taskId;
    private bool _disposed;
    private bool _hasControlState;
    private bool _returnFailed;
    private BrowserControlState _state = BrowserControlState.Automated;

    public BrowserTakeoverViewModel(
        IBrowserTakeoverController controller,
        EmbeddedBrowserWorkspaceViewModel? embeddedBrowsers = null)
    {
        _controller = controller;
        _embeddedBrowsers = embeddedBrowsers;
        HandToUserCommand = new GuardedAsyncRelayCommand(HandToUserAsync, () => CanHandToUser);
        ReturnControlCommand = new GuardedAsyncRelayCommand(ReturnControlAsync, () => CanReturnControl);
        _controller.StateChanged += HandleStateChanged;
    }

    public IAsyncRelayCommand HandToUserCommand { get; }
    public IAsyncRelayCommand ReturnControlCommand { get; }
    public bool HasControlState { get => _hasControlState; private set => SetProperty(ref _hasControlState, value); }
    public bool CanHandToUser => HasControlState && _state == BrowserControlState.Automated;
    public bool CanReturnControl => HasControlState && _state == BrowserControlState.HandedToUser;
    public bool HasStatus => _returnFailed;
    public string Status => _returnFailed ? WorkspaceText.Get("BrowserReturnFailed") : string.Empty;

    public BrowserControlState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value)) NotifyPresentation();
        }
    }

    public string StateLabel => WorkspaceText.Get(_state switch
    {
        BrowserControlState.HandedToUser => "BrowserControlHandedToUser",
        BrowserControlState.ReturningToAutomated => "BrowserControlReturningToAutomated",
        BrowserControlState.Disconnected => "BrowserControlDisconnected",
        _ => "BrowserControlAutomated"
    });

    internal void AttachTask(Guid? taskId)
    {
        if (_taskId == taskId) return;
        _taskId = taskId;
        _returnFailed = false;
        Refresh();
    }

    private async Task HandToUserAsync(CancellationToken cancellationToken)
    {
        if (_taskId is not { } taskId) return;
        try
        {
            await _controller.BeginUserTakeoverAsync(taskId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReturnControlAsync(CancellationToken cancellationToken)
    {
        if (_taskId is not { } taskId) return;
        BrowserControlState result;
        try
        {
            result = await _controller.ReturnControlAsync(taskId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _returnFailed = result != BrowserControlState.Automated;
        Refresh();
    }

    private void HandleStateChanged(object? sender, BrowserControlStateChangedEventArgs args)
    {
        if (_disposed || args.TaskId != _taskId) return;
        _ = AvaloniaUiThread.RunAsync(() =>
        {
            if (_disposed) return;
            var wasAutomated = _state == BrowserControlState.Automated;
            Refresh();
            if (!wasAutomated && _state == BrowserControlState.Automated && _taskId is not null)
            {
                _ = _embeddedBrowsers?.CurrentPane.RefreshTabsAsync();
            }
        }, CancellationToken.None);
    }

    private void Refresh()
    {
        HasControlState = !_disposed && _taskId is { } id && _controller.HasControlState(id);
        State = HasControlState && _taskId is { } taskId
            ? _controller.GetControlState(taskId)
            : BrowserControlState.Automated;
        if (_state != BrowserControlState.HandedToUser)
        {
            _returnFailed = false;
        }

        NotifyPresentation();
    }

    private void NotifyPresentation()
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(CanHandToUser));
        OnPropertyChanged(nameof(CanReturnControl));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasStatus));
        HandToUserCommand.NotifyCanExecuteChanged();
        ReturnControlCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _controller.StateChanged -= HandleStateChanged;
        _taskId = null;
        HasControlState = false;
        ((IDisposable)HandToUserCommand).Dispose();
        ((IDisposable)ReturnControlCommand).Dispose();
    }
}
