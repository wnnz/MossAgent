using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class GitIndexActionsViewModel(IToolExecutor tools, Func<CancellationToken, Task> refresh) : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource _selection = new();
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private GitChangedFileViewModel? _file;
    private GuardedAsyncRelayCommand? _execute;
    private IRelayCommand? _cancel;
    private int _generation;
    private bool _staged;
    private bool _operationStaged;
    private bool _disposed;
    private bool _busy;
    private string _status = string.Empty;

    public GuardedAsyncRelayCommand ExecuteCommand => _execute ??= new(ExecuteAsync, () => CanExecute);
    public IRelayCommand CancelCommand => _cancel ??= new RelayCommand(Cancel, () => CanCancel);
    public bool HasContext => !_disposed && _context is not null;
    public bool IsBusy => _busy;
    public bool CanCancel => !_disposed && _operation is { IsCancellationRequested: false };
    public bool CanExecute => HasContext && !_busy && _context!.ToolsEnabled
        && _context.ApprovalPolicy != MossAgent.Domain.ApprovalPolicy.ReadOnly
        && _file is not null && (_staged ? _file.HasStagedChange : _file.HasUnstagedChange);
    public string ActionLabel => InspectorText.Get(_busy ? _operationStaged ? "DiffUnstaging" : "DiffStaging" : _staged ? "DiffUnstageFile" : "DiffStageFile");
    public string CancelLabel => InspectorText.Get("DiffIndexCancel");
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public void Attach(ToolExecutionContext? context)
    {
        if (_disposed) return;
        _generation++;
        _selection.Cancel();
        _selection.Dispose();
        _selection = new();
        _context = context;
        _file = null;
        Status = string.Empty;
        NotifyState();
    }

    public void Select(GitChangedFileViewModel? file, bool staged)
    {
        _file = file;
        _staged = staged;
        NotifyState();
    }

    private async Task ExecuteAsync(CancellationToken caller)
    {
        if (!CanExecute) return;
        var context = _context! with { AuthorizedRoots = _context!.AuthorizedRoots.ToArray() };
        var file = _file!;
        var generation = _generation;
        _operationStaged = _staged;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token, _selection.Token, _shutdown.Token);
        _operation = operation;
        _busy = true;
        Status = InspectorText.Get(_operationStaged ? "DiffUnstaging" : "DiffStaging");
        NotifyState();
        try
        {
            var originalPath = file.Status.Length == 2 && file.Status[_operationStaged ? 0 : 1] == 'R' ? file.OriginalPath : null;
            var request = new ToolRequest(Guid.NewGuid().ToString("N"), _operationStaged ? "git.unstage_file" : "git.stage_file",
                JsonSerializer.SerializeToElement(new { path = file.Path, originalPath }));
            var result = await tools.ExecuteAsync(request, context, operation.Token).ConfigureAwait(false);
            if (IsCurrent(generation) && !operation.IsCancellationRequested && result.IsSuccess)
            {
                Task? refreshWork = null;
                await AvaloniaUiThread.RunAsync(() =>
                {
                    if (IsCurrent(generation) && !operation.IsCancellationRequested) refreshWork = refresh(operation.Token);
                }, operation.Token).ConfigureAwait(false);
                if (refreshWork is not null) await refreshWork.ConfigureAwait(false);
            }
            await PublishAsync(() =>
            {
                if (IsCurrent(generation)) Status = InspectorText.Get(operation.IsCancellationRequested ? "DiffIndexInterrupted"
                    : result.IsSuccess ? _operationStaged ? "DiffUnstaged" : "DiffStagedFile" : FailureKey(result.ErrorCode));
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (IsCurrent(generation)) Status = InspectorText.Get("DiffIndexInterrupted"); }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (IsCurrent(generation)) Status = InspectorText.Get("DiffIndexFailed"); }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; NotifyState(); }).ConfigureAwait(false);
        }
    }

    private static string FailureKey(string? error) => error switch
    {
        "approval_denied" or "capability_denied" => "DiffIndexDenied",
        "path_denied" or "workspace_denied" => "DiffIndexPathDenied",
        "tool_cancelled" or "tool_timeout" => "DiffIndexInterrupted",
        _ => "DiffIndexFailed"
    };

    private bool IsCurrent(int generation) => !_disposed && generation == _generation;

    private async Task PublishAsync(Action publish)
    {
        if (_disposed) return;
        try { await AvaloniaUiThread.RunAsync(() => { if (!_disposed) publish(); }, _shutdown.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private void Cancel()
    {
        if (!CanCancel) return;
        _operation!.Cancel();
        Status = InspectorText.Get("DiffIndexCancelling");
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var name in new[] { nameof(HasContext), nameof(IsBusy), nameof(CanExecute), nameof(CanCancel), nameof(ActionLabel) }) OnPropertyChanged(name);
        _execute?.NotifyCanExecuteChanged();
        _cancel?.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _shutdown.Cancel();
        _selection.Cancel();
        _execute?.Dispose();
        NotifyState();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (_execute?.ExecutionTask is { } work) await work.ConfigureAwait(false);
        _shutdown.Dispose();
        _selection.Dispose();
    }
}
