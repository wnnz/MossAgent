using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class ApprovalTaskControlViewModel : ObservableObject, IDisposable
{
    private readonly ApprovalViewModel _approval;
    private readonly WorkspaceViewModel _workspace;
    private bool _disposed;

    public ApprovalTaskControlViewModel(ApprovalViewModel approval, WorkspaceViewModel workspace)
    {
        _approval = approval;
        _workspace = workspace;
        StopCommand = new RelayCommand<Guid?>(Stop, CanStopRequest);
        approval.PropertyChanged += StateChanged;
        workspace.PropertyChanged += StateChanged;
        workspace.Catalog.PropertyChanged += StateChanged;
    }

    public IRelayCommand<Guid?> StopCommand { get; }
    public bool CanStop => CanStopRequest(_approval.CurrentRequestId);

    private bool CanStopRequest(Guid? requestId) => !_disposed && requestId is not null
        && !_workspace.IsShuttingDown && _workspace.HasActiveRun
        && _approval.GetPendingTaskId(requestId) is { } taskId
        && _workspace.Catalog.SelectedTask?.Id == taskId
        && _workspace.PrimaryActionCommand.CanExecute(null);

    private void Stop(Guid? requestId)
    {
        if (CanStopRequest(requestId)) _workspace.PrimaryActionCommand.Execute(null);
    }

    private void StateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(CanStop));
        StopCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _approval.PropertyChanged -= StateChanged;
        _workspace.PropertyChanged -= StateChanged;
        _workspace.Catalog.PropertyChanged -= StateChanged;
        OnPropertyChanged(nameof(CanStop));
        StopCommand.NotifyCanExecuteChanged();
    }
}
