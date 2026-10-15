using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

public sealed class BrowserPaneActionsViewModel : ObservableObject, IDisposable
{
    private readonly Guid? _taskId;
    private readonly IToolExecutor? _executor;
    private readonly SettingsOperationLifetime _lifetime;
    private ToolExecutionContext? _context;
    private IBrowserSession? _session;
    private CancellationTokenSource? _operation;
    private long _generation;
    private bool _disposed;
    private bool _isBusy;
    private bool _cancelRequested;
    private string _status = string.Empty;

    public BrowserPaneActionsViewModel(Guid? taskId, IToolExecutor? executor, TimeSpan? timeout = null)
    {
        _taskId = taskId;
        _executor = executor;
        _lifetime = new(timeout ?? TimeSpan.FromMinutes(5));
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
    }

    public event EventHandler? StateChanged;
    public IRelayCommand CancelCommand { get; }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool CanExecute => !_disposed && !IsBusy && _executor is not null && _context is not null && _session is not null;
    public bool CanCancel => !_disposed && _operation is { IsCancellationRequested: false };

    public void SetContext(ToolExecutionContext? context)
    {
        if (context is not null && (_taskId is null || _taskId == Guid.Empty || context.TaskId != _taskId))
        {
            SetContext(null);
            throw new ArgumentException("The browser context must belong to its owning task.", nameof(context));
        }
        if (_disposed || ReferenceEquals(_context, context)) return;
        Invalidate();
        _context = context;
        NotifyState();
    }

    public void SetSession(IBrowserSession? session)
    {
        if (_disposed || ReferenceEquals(_session, session)) return;
        Invalidate();
        _session = session;
        NotifyState();
    }

    internal async Task<bool> ExecuteAsync(string toolName, object arguments, CancellationToken cancellationToken,
        Func<CancellationToken, Task<bool>>? refresh = null)
    {
        if (!CanExecute) return false;
        var generation = _generation;
        var context = _context! with { BrowserSession = _session };
        using var operation = _lifetime.CreateOperation(cancellationToken);
        _operation = operation;
        _cancelRequested = false;
        IsBusy = true;
        Status = WorkspaceText.Get("BrowserActionRunning");
        NotifyState();
        var succeeded = false;
        try
        {
            var request = new ToolRequest(Guid.NewGuid().ToString("N"), toolName, JsonSerializer.SerializeToElement(arguments));
            var result = await _executor!.ExecuteAsync(request, context, operation.Token);
            if (result.IsSuccess && !operation.IsCancellationRequested && IsCurrent(generation) && refresh is not null
                && !await refresh(operation.Token)) result = ToolResult.Failure(string.Empty, "browser_tabs_refresh_failed");
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (!IsCurrent(generation)) return;
                succeeded = result.IsSuccess && !operation.IsCancellationRequested;
                Status = WorkspaceText.Get(ResultKey(result, operation, cancellationToken));
            }, CancellationToken.None);
        }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (IsCurrent(generation)) Status = WorkspaceText.Get(ResultKey(null, operation, cancellationToken));
            }, CancellationToken.None);
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                _operation = null;
                IsBusy = false;
                NotifyState();
            }, CancellationToken.None);
        }
        return succeeded;
    }

    private string ResultKey(ToolResult? result, CancellationTokenSource operation, CancellationToken caller)
    {
        if (operation.IsCancellationRequested)
            return _cancelRequested || caller.IsCancellationRequested ? "BrowserActionCancelled" : "BrowserActionTimedOut";
        if (result?.IsSuccess == true) return "BrowserActionCompleted";
        return result?.ErrorCode switch
        {
            "capability_denied" or "approval_denied" => "BrowserActionDenied",
            "tool_cancelled" => "BrowserActionCancelled",
            "tool_timeout" => "BrowserActionTimedOut",
            "browser_devtools_unsupported" => "BrowserDevToolsUnsupported",
            "browser_tabs_refresh_failed" => "BrowserTabsRefreshFailed",
            _ => "BrowserActionFailed"
        };
    }

    private bool IsCurrent(long generation) => !_disposed && generation == _generation;

    private void Cancel()
    {
        if (!CanCancel) return;
        _cancelRequested = true;
        Status = WorkspaceText.Get("BrowserActionCancelling");
        _operation!.Cancel();
        NotifyState();
    }

    private void Invalidate()
    {
        _generation++;
        _operation?.Cancel();
        Status = string.Empty;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(CanCancel));
        CancelCommand.NotifyCanExecuteChanged();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Invalidate();
        _context = null;
        _session = null;
        _lifetime.Dispose();
        NotifyState();
    }
}
