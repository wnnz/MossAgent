using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class ApprovalViewModel : ObservableObject
{
    private readonly Lock _lock = new();
    private TaskCompletionSource<bool>? _pending;
    private bool _isPending;
    private string _toolName = string.Empty;
    private string _description = string.Empty;

    public ApprovalViewModel()
    {
        ApproveCommand = new RelayCommand(() => Resolve(true), () => IsPending);
        DenyCommand = new RelayCommand(() => Resolve(false), () => IsPending);
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
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (_pending is not null)
            {
                throw new InvalidOperationException("已有工具调用正在等待审批。");
            }

            _pending = completion;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ToolName = descriptor.Name;
            Description = descriptor.Description;
            IsPending = true;
            NotifyCommands();
        });

        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try
        {
            return await completion.Task;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(Clear);
        }
    }

    private void Resolve(bool approved)
    {
        lock (_lock)
        {
            _pending?.TrySetResult(approved);
        }
    }

    private void Clear()
    {
        lock (_lock)
        {
            _pending = null;
        }

        IsPending = false;
        ToolName = string.Empty;
        Description = string.Empty;
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged();
    }
}
