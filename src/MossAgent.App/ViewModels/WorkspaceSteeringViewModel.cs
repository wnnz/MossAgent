using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Application.Agent;
using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSteeringViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceRunTracker _runs;
    private readonly WorkspaceRunController _controller;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly WorkspaceComposerDraftsViewModel _drafts;
    private readonly IRelayCommand _primary;
    private readonly Func<bool> _canAct;
    private readonly WorkspaceFollowupOperations _operations = new();
    private readonly Dictionary<Guid, WorkspaceSteeringAttempt> _attempts = [];
    private readonly Dictionary<Guid, string> _notices = [];
    private bool _disposed;

    internal WorkspaceSteeringViewModel(IConversationSteeringRepository? repository, WorkspaceRunTracker runs,
        WorkspaceRunController controller, WorkspaceCatalogViewModel catalog, WorkspaceComposerDraftsViewModel drafts,
        IRelayCommand primary, IRelayCommand fallbackSubmit, Func<bool> canAct)
    {
        IsAvailable = repository is not null; _runs = runs; _controller = controller; _catalog = catalog;
        _drafts = drafts; _primary = primary; _canAct = canAct;
        History = new(repository, catalog);
        History.PropertyChanged += HandleDraftChanged;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        AlternateSubmitCommand = new RelayCommand(() =>
        {
            if (HasActiveRun && IsAvailable) SubmitCommand.Execute(null); else fallbackSubmit.Execute(null);
        }, () => HasActiveRun && IsAvailable ? SubmitCommand.CanExecute(null) : fallbackSubmit.CanExecute(null));
        _drafts.PropertyChanged += HandleDraftChanged;
        _primary.CanExecuteChanged += HandlePrimaryChanged;
        _catalog.SelectionChanged += NotifyStateChanged;
        _controller.RunFinished += HandleFinishedAsync;
        _operations.StateChanged += HandleOperationsChangedAsync;
    }

    public WorkspaceSteeringHistoryViewModel History { get; }
    public bool IsAvailable { get; }
    public bool HasActiveRun => _catalog.SelectedTask is { } task && _runs.IsRunning(task.Id);
    public bool IsVisible => IsAvailable && (HasActiveRun || History.IsVisible || HasNotice);
    public bool IsBusy => _catalog.SelectedTask is { } task && _operations.IsPreparing(task.Id);
    public bool CanSubmit => !_disposed && IsAvailable && !_operations.IsStopping && !IsBusy && _canAct()
        && _catalog.SelectedTask is { } task && task.ProjectId == _catalog.SelectedProject?.Id
        && _drafts.Scope == new ConversationDraftScope(task.ProjectId, task.Id) && _drafts.CanSend
        && !string.IsNullOrWhiteSpace(_drafts.Text)
        && _drafts.Text.Length <= ConversationSteeringInput.MaximumTextLength
        && _runs.TryGet(task.Id, out var state) && state?.Steering is { IsAccepting: true } session && _drafts.CanSteerTo(session);
    public string SubmitLabel => WorkspaceText.Get(IsBusy ? "SteeringReceiving" : "SteeringSubmit");
    public string Hint => WorkspaceText.Get(_drafts.Text.Length > ConversationSteeringInput.MaximumTextLength
        ? "SteeringTooLong" : "SteeringHint");
    public bool HasNotice => _catalog.SelectedTask is { } task && _notices.ContainsKey(task.Id);
    public string Notice => _catalog.SelectedTask is { } task && _notices.TryGetValue(task.Id, out var key) ? WorkspaceText.Get(key) : string.Empty;
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand AlternateSubmitCommand { get; }

    private Task SubmitAsync()
    {
        if (!CanSubmit || _catalog.SelectedTask is not { } task || !_runs.TryGet(task.Id, out var state) || state?.Steering is not { } session)
            return Task.CompletedTask;
        var scope = _drafts.Scope;
        var text = _drafts.Text;
        if (!_attempts.TryGetValue(task.Id, out var attempt) || attempt.RunId != session.RunId || attempt.Scope != scope || attempt.Text != text)
            _attempts[task.Id] = attempt = new(session.RunId, scope, text, Guid.NewGuid());
        var captured = attempt;
        return _operations.RunAsync(task.Id, true, async token =>
        {
            try
            {
                var submission = _drafts.CreateSteeringSubmission(session, scope, text);
                var receipt = await submission.SaveAsync(session, captured.RunId, captured.Id, token);
                await AvaloniaUiThread.RunAsync(() =>
                {
                    if (_disposed) return;
                    if (!receipt.AlreadyAccepted) { _attempts.Remove(task.Id); _notices.Remove(task.Id); }
                    else _notices[task.Id] = "SteeringAlreadyAccepted";
                }, CancellationToken.None);
            }
            catch (Exception exception)
            {
                await AvaloniaUiThread.RunAsync(() => { if (!_disposed) _notices[task.Id] = FailureKey(exception); }, CancellationToken.None);
            }
            await History.RefreshForTaskAsync(task.Id);
        });
    }

    private static string FailureKey(Exception exception) => exception switch
    {
        ConversationDraftConflictException => "DraftConflict",
        ConversationSteeringException failure => failure.Code,
        SourceReviewException or UnauthorizedAccessException => "SteeringScopeChanged",
        ModelCapabilityException => "SteeringModelUnsupported",
        ConversationAttachmentUnavailableException or IOException or InvalidDataException => "SteeringAttachmentUnavailable",
        OperationCanceledException => "SteeringStopped",
        ArgumentException => "SteeringInvalid",
        _ => "SteeringFailed"
    };

    internal void NotifyRunEvent(Guid taskId, AgentEvent agentEvent)
    {
        if (_disposed || agentEvent is not (AgentStatusEvent or AgentSteeringAppliedEvent or AgentCompletedEvent or AgentFailureEvent or AgentCancelledEvent)) return;
        if (agentEvent is AgentSteeringAppliedEvent) _ = History.RefreshForTaskAsync(taskId);
        NotifyStateChanged();
    }

    internal void NotifyStateChanged()
    {
        if (_disposed) return;
        foreach (var name in new[] { nameof(IsVisible), nameof(HasActiveRun), nameof(IsBusy), nameof(CanSubmit), nameof(SubmitLabel), nameof(Hint), nameof(HasNotice), nameof(Notice) }) OnPropertyChanged(name);
        SubmitCommand.NotifyCanExecuteChanged(); AlternateSubmitCommand.NotifyCanExecuteChanged();
    }
    private void HandleDraftChanged(object? sender, PropertyChangedEventArgs args) => NotifyStateChanged();
    private void HandlePrimaryChanged(object? sender, EventArgs args) => NotifyStateChanged();
    private Task HandleOperationsChangedAsync() => AvaloniaUiThread.RunAsync(NotifyStateChanged, CancellationToken.None);
    private Task HandleFinishedAsync(WorkspaceRunState state, bool released) => AvaloniaUiThread.RunAsync(async () =>
    {
        if (_disposed) return;
        if (state.AuthorizationChanged) _notices[state.Task.Id] = "SteeringScopeChanged";
        NotifyStateChanged();
        await History.RefreshForTaskAsync(state.Task.Id);
    }, CancellationToken.None);
    public Task StopAsync() => Task.WhenAll(_operations.StopAsync(), History.StopAsync());
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _operations.StateChanged -= HandleOperationsChangedAsync;
        _catalog.SelectionChanged -= NotifyStateChanged; _primary.CanExecuteChanged -= HandlePrimaryChanged;
        _drafts.PropertyChanged -= HandleDraftChanged; _controller.RunFinished -= HandleFinishedAsync;
        History.PropertyChanged -= HandleDraftChanged; _operations.Dispose(); History.Dispose();
    }
}
