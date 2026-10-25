using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Artifacts;
using MossAgent.App.Resources;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

public sealed class BrowserProfileApprovalViewModel : ObservableObject, IBrowserProfileAccessApproval
{
    private readonly Lock _lock = new();
    private readonly Queue<BrowserProfileApprovalRequest> _queue = [];
    private BrowserProfileApprovalRequest? _current;
    private bool _isPending;
    private string _description = string.Empty;
    private string _taskLabel = string.Empty;
    private int _pendingCount;

    public BrowserProfileApprovalViewModel()
    {
        ApproveCommand = new RelayCommand(() => ResolveCurrent(true));
        DenyCommand = new RelayCommand(() => ResolveCurrent(false));
    }

    public IRelayCommand ApproveCommand { get; }
    public IRelayCommand DenyCommand { get; }
    public bool IsPending { get => _isPending; private set => SetProperty(ref _isPending, value); }
    public string Title => ApprovalText.BrowserProfileTitle;
    public string Description { get => _description; private set => SetProperty(ref _description, value); }
    public string TaskLabel { get => _taskLabel; private set => SetProperty(ref _taskLabel, value); }
    public string QueueLabel => _pendingCount > 1 ? ApprovalText.Format("Queue", _pendingCount - 1) : string.Empty;
    public bool HasQueuedRequests => _pendingCount > 1;

    public async ValueTask<bool> ConfirmAsync(
        Guid taskId,
        BrowserProfileMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = new BrowserProfileApprovalRequest(taskId, mode);
        lock (_lock)
        {
            _queue.Enqueue(pending);
            if (_current is null) ActivateNextLocked();
        }

        using var registration = cancellationToken.Register(() => Cancel(pending, cancellationToken));
        await RefreshPresentationAsync();
        return await pending.Completion.Task;
    }

    private void ResolveCurrent(bool approved)
    {
        BrowserProfileApprovalRequest? current;
        lock (_lock)
        {
            current = _current;
            if (current is null || current.Completion.Task.IsCompleted) return;
            current.Completion.TrySetResult(approved);
            _current = null;
            ActivateNextLocked();
        }

        _ = RefreshPresentationAsync();
    }

    private void Cancel(BrowserProfileApprovalRequest pending, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            pending.Completion.TrySetCanceled(cancellationToken);
            if (ReferenceEquals(_current, pending))
            {
                _current = null;
                ActivateNextLocked();
            }
            else
            {
                var count = _queue.Count;
                for (var index = 0; index < count; index++)
                {
                    var candidate = _queue.Dequeue();
                    if (!ReferenceEquals(candidate, pending)) _queue.Enqueue(candidate);
                }
            }
        }

        _ = RefreshPresentationAsync();
    }

    private void ActivateNextLocked()
    {
        while (_queue.TryDequeue(out var candidate))
        {
            if (!candidate.Completion.Task.IsCompleted)
            {
                _current = candidate;
                return;
            }
        }

        _current = null;
    }

    private async Task RefreshPresentationAsync()
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            RefreshPresentation();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(RefreshPresentation);
    }

    private void RefreshPresentation()
    {
        BrowserProfileApprovalRequest? current;
        int pendingCount;
        lock (_lock)
        {
            current = _current;
            pendingCount = _queue.Count + (current is null ? 0 : 1);
        }

        _pendingCount = pendingCount;
        Description = current is null ? string.Empty
            : ApprovalText.Format(current.Mode == BrowserProfileMode.DailyProfile
                ? "BrowserProfileDaily" : "BrowserProfileCdp");
        TaskLabel = current is null ? string.Empty : ApprovalText.Format("Task", current.TaskId);
        IsPending = current is not null;
        OnPropertyChanged(nameof(QueueLabel));
        OnPropertyChanged(nameof(HasQueuedRequests));
        ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged();
    }
}
