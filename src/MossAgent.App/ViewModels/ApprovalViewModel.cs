using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class ApprovalViewModel : ObservableObject
{
    private readonly Lock _lock = new();
    private readonly Queue<ToolApprovalRequestViewModel> _queue = [];
    private ToolApprovalRequestViewModel? _current;
    private bool _isPending;
    private string _toolName = string.Empty;
    private string _description = string.Empty;

    public ApprovalViewModel()
    {
        ApproveCommand = new RelayCommand(() => ResolveCurrent(true), () => IsPending);
        DenyCommand = new RelayCommand(() => ResolveCurrent(false), () => IsPending);
    }

    public IRelayCommand ApproveCommand { get; }
    public IRelayCommand DenyCommand { get; }
    public bool IsPending { get => _isPending; private set => SetProperty(ref _isPending, value); }
    public string ToolName { get => _toolName; private set => SetProperty(ref _toolName, value); }
    public string Description { get => _description; private set => SetProperty(ref _description, value); }

    public async ValueTask<bool> RequestAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = request;
        var pending = new ToolApprovalRequestViewModel(descriptor);
        var becameCurrent = Enqueue(pending);

        using var registration = cancellationToken.Register(
            () => Cancel(pending, cancellationToken));
        if (becameCurrent)
        {
            await RefreshPresentationAsync();
        }

        return await pending.Completion.Task;
    }

    private bool Enqueue(ToolApprovalRequestViewModel pending)
    {
        lock (_lock)
        {
            _queue.Enqueue(pending);
            if (_current is not null)
            {
                return false;
            }

            ActivateNextLocked();
            return ReferenceEquals(_current, pending);
        }
    }

    private void ResolveCurrent(bool approved)
    {
        lock (_lock)
        {
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
        lock (_lock)
        {
            current = _current;
        }

        IsPending = current is not null;
        ToolName = current?.ToolName ?? string.Empty;
        Description = current?.Description ?? string.Empty;
        ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged();
    }
}
