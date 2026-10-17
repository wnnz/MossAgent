using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class ApprovalViewModel : ObservableObject
{
    private readonly Lock _lock = new();
    private readonly Queue<ToolApprovalRequestViewModel> _queue = [];
    private ToolApprovalRequestViewModel? _current;
    private bool _isPending;
    private string _toolName = string.Empty;
    private string _description = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _summary = string.Empty;
    private string _taskLabel = string.Empty;
    private string _riskLabel = string.Empty;
    private Guid? _currentRequestId;
    private int _pendingCount;
    private ToolApprovalContentViewModel? _contentPreview;

    public ApprovalViewModel()
    {
        ApproveCommand = new RelayCommand<Guid?>(requestId => ResolveCurrent(requestId, true), CanResolve);
        DenyCommand = new RelayCommand<Guid?>(requestId => ResolveCurrent(requestId, false), CanResolve);
    }

    public IRelayCommand<Guid?> ApproveCommand { get; }
    public IRelayCommand<Guid?> DenyCommand { get; }
    public bool IsPending { get => _isPending; private set => SetProperty(ref _isPending, value); }
    public string ToolName { get => _toolName; private set => SetProperty(ref _toolName, value); }
    public string Description { get => _description; private set => SetProperty(ref _description, value); }
    public string WorkingDirectory { get => _workingDirectory; private set => SetProperty(ref _workingDirectory, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string TaskLabel { get => _taskLabel; private set => SetProperty(ref _taskLabel, value); }
    public string RiskLabel { get => _riskLabel; private set => SetProperty(ref _riskLabel, value); }
    public Guid? CurrentRequestId { get => _currentRequestId; private set => SetProperty(ref _currentRequestId, value); }
    public ToolApprovalContentViewModel? ContentPreview { get => _contentPreview; private set => SetProperty(ref _contentPreview, value); }
    public string QueueLabel => PendingCount > 1 ? ApprovalText.Format("Queue", PendingCount - 1) : string.Empty;
    public bool HasQueuedRequests => PendingCount > 1;
    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (!SetProperty(ref _pendingCount, value)) return;
            OnPropertyChanged(nameof(QueueLabel));
            OnPropertyChanged(nameof(HasQueuedRequests));
        }
    }

    public ValueTask<bool> RequestAsync(ToolDescriptor descriptor, ToolRequest request, CancellationToken cancellationToken) =>
        RequestAsync(descriptor, request, null, cancellationToken);

    public async ValueTask<bool> RequestAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext? context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = new ToolApprovalRequestViewModel(descriptor, request, context);
        Enqueue(pending);

        using var registration = cancellationToken.Register(
            () => Cancel(pending, cancellationToken));
        await RefreshPresentationAsync();

        return await pending.Completion.Task;
    }

    private void Enqueue(ToolApprovalRequestViewModel pending)
    {
        lock (_lock)
        {
            _queue.Enqueue(pending);
            if (_current is not null)
            {
                return;
            }

            ActivateNextLocked();
        }
    }

    private bool CanResolve(Guid? requestId)
    {
        lock (_lock)
            return _current is not null && !_current.Completion.Task.IsCompleted
                && requestId == _current.RequestId;
    }

    internal Guid? GetPendingTaskId(Guid? requestId)
    {
        lock (_lock)
            return _current is { } current && requestId == current.RequestId && !current.Completion.Task.IsCompleted
                ? current.TaskId : null;
    }

    private void ResolveCurrent(Guid? requestId, bool approved)
    {
        lock (_lock)
        {
            if (_current is null || requestId != _current.RequestId) return;
            _current?.Completion.TrySetResult(approved);
            _current = null;
            ActivateNextLocked();
        }

        _ = RefreshPresentationAsync();
    }

    private void Cancel(
        ToolApprovalRequestViewModel pending,
        CancellationToken cancellationToken)
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
                RemoveQueuedLocked(pending);
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

    private void RemoveQueuedLocked(ToolApprovalRequestViewModel pending)
    {
        var count = _queue.Count;
        for (var index = 0; index < count; index++)
        {
            var candidate = _queue.Dequeue();
            if (!ReferenceEquals(candidate, pending))
            {
                _queue.Enqueue(candidate);
            }
        }
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
        ToolApprovalRequestViewModel? current;
        int pendingCount;
        lock (_lock)
        {
            current = _current;
            pendingCount = _queue.Count + (current is null ? 0 : 1);
        }

        if (!ReferenceEquals(ContentPreview, current?.ContentPreview)) ContentPreview?.Close();
        ContentPreview = current?.ContentPreview;
        IsPending = current is not null;
        ToolName = current?.ToolName ?? string.Empty;
        Description = current?.Description ?? string.Empty;
        WorkingDirectory = current?.WorkingDirectory ?? string.Empty;
        Summary = current?.Summary ?? string.Empty;
        TaskLabel = current?.TaskId is Guid taskId ? ApprovalText.Format("Task", taskId)
            : current is null ? string.Empty : ApprovalText.Get("ContextUnavailable");
        RiskLabel = current?.RiskLabel ?? string.Empty;
        PendingCount = pendingCount;
        CurrentRequestId = current?.RequestId;
        ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged();
    }
}
