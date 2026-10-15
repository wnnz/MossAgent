using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Services;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFileChangeUndoViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly WorkspaceTaskPanelCoordinator _panels;
    private readonly WorkspaceFileChangesViewModel _summary;
    private readonly WorkspaceFileChangeUndoService? _service;
    private WorkspaceFileUndoPlan? _confirmation;
    private WorkspaceFileUndoPlan? _runningPlan;
    private ConversationMessage? _unsaved;
    private Guid? _ownedTask;
    private string _status = string.Empty;
    private bool _busy;
    private bool _disposed;
    private ApprovalPolicy? _policy;

    public WorkspaceFileChangeUndoViewModel(WorkspaceCatalogViewModel catalog, WorkspaceTaskPanelCoordinator panels,
        WorkspaceFileChangesViewModel summary, WorkspaceFileChangeUndoService? service)
    {
        _catalog = catalog;
        _panels = panels;
        _summary = summary;
        _service = service;
        RequestCommand = new RelayCommand(Request, CanStart);
        ConfirmCommand = new GuardedAsyncRelayCommand(ExecuteAsync, () => _confirmation is not null && CanStart());
        CancelCommand = new RelayCommand(Cancel, () => !_disposed && (_busy || _confirmation is not null || _unsaved is null));
        RetrySaveCommand = new GuardedAsyncRelayCommand(RetrySaveAsync, () => !_disposed && !_busy && _unsaved is not null);
        catalog.SelectionChanged += SelectionChanged;
        catalog.TaskRunStateChanged += AvailabilityChanged;
        catalog.PropertyChanged += CatalogChanged;
        catalog.TaskManagement.PropertyChanged += CatalogChanged;
        summary.PropertyChanged += SummaryChanged;
    }

    public event Action? StateChanged;
    public IRelayCommand RequestCommand { get; }
    public GuardedAsyncRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public GuardedAsyncRelayCommand RetrySaveCommand { get; }
    public bool IsBusy => _busy;
    public bool IsConfirming => _confirmation is not null;
    public bool HasUnsavedRecord => _unsaved is not null;
    public bool HasNotice => _busy || IsConfirming || HasUnsavedRecord || _status.Length > 0;
    public bool CanConfirm => IsConfirming && !_busy;
    public ApprovalPolicy? SelectedApprovalPolicy
    {
        get => _policy;
        set { if (SetProperty(ref _policy, value)) AvailabilityChanged(); }
    }
    private ApprovalPolicy ActivePolicy => _policy ?? _catalog.SelectedTask?.ApprovalPolicy ?? ApprovalPolicy.ReadOnly;
    public string ConfirmLabel => WorkspaceText.Get("FileUndoConfirm");
    public string CancelLabel => WorkspaceText.Get(_busy || IsConfirming ? "FileUndoCancel" : "FileUndoDismiss");
    public string RetrySaveLabel => WorkspaceText.Get("FileUndoRetrySave");
    public string Notice => _busy ? WorkspaceText.Get("FileUndoRunning") : _confirmation is { } plan
        ? string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("FileUndoConfirmation"),
            plan.Changes.Count, plan.Changes.Count(change => change.BeforeSha256 is null)) : _status;
    public string Hint => WorkspaceText.Get(_service is null ? "FileChangesUndoUnavailable"
        : HasUnsavedRecord ? "FileUndoSaveFailed" : ActivePolicy == ApprovalPolicy.ReadOnly ? "FileUndoReadOnly" : "FileUndoHint");
    public bool OwnsTask(Guid taskId) => _busy && _ownedTask == taskId;

    private bool CanStart() => !_disposed && !_busy && _service is not null && _unsaved is null
        && !_catalog.IsLocked && !_summary.IsIncomplete && _summary.Files.Count > 0
        && _catalog.SelectedTask is { } task && _catalog.SelectedProject?.Id == task.ProjectId
        && _catalog.IsTaskRunning?.Invoke(task.Id) != true && !_catalog.TaskManagement.BlocksTaskStart(task.Id)
        && _summary.Files.All(file => file.Change.IsValid
            && (file.Change.BeforeSha256 is null || file.Change.BeforeSnapshot is not null)
            && (file.Change.AfterSha256 is null || file.Change.AfterSnapshot is not null))
        && ActivePolicy != ApprovalPolicy.ReadOnly;

    private void Request()
    {
        if (!CanStart()) return;
        var task = _catalog.SelectedTask!;
        var project = _catalog.SelectedProject!;
        var context = _panels.CreateContext(task, project);
        _confirmation = new(task, project, context with { AuthorizedRoots = context.AuthorizedRoots.ToArray(), ApprovalPolicy = ActivePolicy },
            _summary.Files.Select(file => file.Change).ToArray());
        _status = string.Empty;
        NotifyState();
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_confirmation is not { } plan || !MatchesScope(plan)
            || !plan.Changes.SequenceEqual(_summary.Files.Select(file => file.Change))) { Cancel(); return; }
        _confirmation = null;
        _runningPlan = plan;
        SetBusy(true, plan.Task.Id);
        try
        {
            var outcome = await Task.Run(() => _service!.ExecuteAsync(plan,
                change => AvaloniaUiThread.RunAsync(() => !_disposed && !_summary.IsIncomplete && MatchesScope(plan)
                    && _summary.Files.Any(file => file.Change == change), cancellationToken),
                message => AvaloniaUiThread.RunAsync(() => Publish(plan, message), CancellationToken.None), cancellationToken));
            _unsaved = outcome.UnsavedRecord;
            _status = outcome.UnsavedRecord is null && !MatchesScope(plan) ? string.Empty : WorkspaceText.Get(outcome.StatusKey)
                + (string.IsNullOrWhiteSpace(outcome.Detail) ? string.Empty : $"\n{outcome.Detail}");
        }
        catch (OperationCanceledException) { _status = MatchesScope(plan) ? WorkspaceText.Get("FileUndoStopped") : string.Empty; }
        catch { _status = MatchesScope(plan) ? WorkspaceText.Get("FileUndoFailed") : string.Empty; }
        finally { _runningPlan = null; SetBusy(false, null); }
    }

    private bool MatchesScope(WorkspaceFileUndoPlan plan) => _catalog.SelectedTask == plan.Task
        && _catalog.SelectedProject == plan.Project && !_catalog.IsLocked && ActivePolicy == plan.Context.ApprovalPolicy;

    private void Publish(WorkspaceFileUndoPlan plan, ConversationMessage message)
    {
        if (_disposed || _catalog.SelectedTask?.Id != message.TaskId || _catalog.SelectedProject?.Id != plan.Project.Id) return;
        if (_catalog.Messages.All(existing => existing.ToolCallId != message.ToolCallId))
            _catalog.Messages.Add(WorkspaceConversationMapper.ToChatMessage(message));
    }

    private async Task RetrySaveAsync(CancellationToken cancellationToken)
    {
        if (_unsaved is not { } message || _service is null) return;
        SetBusy(true, message.TaskId);
        try
        {
            if (await _service.SaveAsync(message))
            {
                _unsaved = null;
                _status = WorkspaceText.Get("FileUndoSaved");
            }
            else _status = WorkspaceText.Get("FileUndoSaveFailed");
        }
        finally { SetBusy(false, null); }
    }

    private void Cancel()
    {
        _confirmation = null;
        ConfirmCommand.Cancel();
        RetrySaveCommand.Cancel();
        if (!_busy && _unsaved is null) _status = string.Empty;
        NotifyState();
    }

    private void SelectionChanged()
    {
        _policy = null;
        Cancel();
        AvailabilityChanged();
    }

    private void SummaryChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(WorkspaceFileChangesViewModel.VisibleFiles) or nameof(WorkspaceFileChangesViewModel.IsIncomplete))) return;
        if (_confirmation is { } plan && !plan.Changes.SequenceEqual(_summary.Files.Select(file => file.Change))) _confirmation = null;
        NotifyState();
    }

    private void CatalogChanged(object? sender, PropertyChangedEventArgs args) => AvailabilityChanged();

    private void AvailabilityChanged()
    {
        if (_confirmation is { } plan && (!CanStart() || !MatchesScope(plan))) _confirmation = null;
        if (_busy && (_catalog.IsLocked || _runningPlan is { } active && !MatchesScope(active))) ConfirmCommand.Cancel();
        NotifyState();
    }

    private void SetBusy(bool busy, Guid? taskId)
    {
        _busy = busy;
        _ownedTask = taskId;
        NotifyState();
        StateChanged?.Invoke();
    }

    private void NotifyState()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(IsConfirming), nameof(HasUnsavedRecord), nameof(HasNotice),
            nameof(CanConfirm), nameof(Notice), nameof(Hint), nameof(CancelLabel) }) OnPropertyChanged(name);
        RequestCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RetrySaveCommand.NotifyCanExecuteChanged();
    }

    public async Task StopAsync()
    {
        Cancel();
        if (ConfirmCommand.ExecutionTask is { } operation) await operation;
        if (RetrySaveCommand.ExecutionTask is { } retry) await retry;
        if (_unsaved is { } message && _service is not null)
        {
            if (!await _service.SaveAsync(message)) throw new InvalidOperationException(WorkspaceText.Get("FileUndoSaveFailed"));
            _unsaved = null;
            NotifyState();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.SelectionChanged -= SelectionChanged;
        _catalog.TaskRunStateChanged -= AvailabilityChanged;
        _catalog.PropertyChanged -= CatalogChanged;
        _catalog.TaskManagement.PropertyChanged -= CatalogChanged;
        _summary.PropertyChanged -= SummaryChanged;
        ConfirmCommand.Dispose();
        RetrySaveCommand.Dispose();
        RequestCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        StateChanged = null;
    }
}
