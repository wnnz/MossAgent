using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFollowupQueueViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceFollowupRuntime? _runtime;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly WorkspaceFollowupOperations _actions = new();
    private readonly Dictionary<Guid, WorkspaceFollowupRowViewModel> _editors = [];
    private readonly Func<bool> _canEnqueue;
    private readonly Func<bool> _canContinue;
    private readonly IRelayCommand _primary;
    private ConversationFollowupQueue? _queue;
    private string? _notice;
    private int _generation;
    private bool _disposed;

    internal WorkspaceFollowupQueueViewModel(WorkspaceFollowupRuntime? runtime, WorkspaceCatalogViewModel catalog,
        Func<WorkspaceSendRequest> capture, Func<bool> canEnqueue, Func<bool> canContinue, Func<bool> isRunning, IRelayCommand primary)
    {
        _runtime = runtime; _catalog = catalog; _canEnqueue = canEnqueue; _canContinue = canContinue; _primary = primary;
        EnqueueCommand = new AsyncRelayCommand(() =>
        {
            var request = capture();
            return ActAsync(() => _runtime!.EnqueueAsync(request));
        }, CanEnqueue);
        ContinueCommand = new AsyncRelayCommand(() =>
        {
            var taskId = _queue!.TaskId;
            return ActAsync(() => _runtime!.ContinueAsync(taskId));
        }, CanContinue);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => IsAvailable && !_disposed);
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync, () => Editor is not null && !IsBusy);
        CancelEditCommand = new RelayCommand(CancelEdit, () => Editor is not null && !IsBusy);
        SubmitCommand = new RelayCommand(() =>
        {
            if (isRunning()) EnqueueCommand.Execute(null); else primary.Execute(null);
        }, () => isRunning() ? EnqueueCommand.CanExecute(null) : primary.CanExecute(null));
        _actions.StateChanged += HandleStateChangedAsync;
        if (_runtime is not null) { _runtime.Changed += HandleChanged; _runtime.StateChanged += HandleRuntimeStateChanged; }
        _primary.CanExecuteChanged += HandlePrimaryChanged;
        _catalog.SelectionChanged += SelectTask;
    }

    public event Action? StateChanged;
    public ObservableCollection<WorkspaceFollowupRowViewModel> Items { get; } = [];
    public WorkspaceFollowupRowViewModel? Editor => _catalog.SelectedTask is { } task ? _editors.GetValueOrDefault(task.Id) : null;
    public bool HasEditor => Editor is not null;
    public bool IsAvailable => _runtime is not null;
    public bool CanEnqueueMessage => CanEnqueue();
    public bool HasItems => Items.Count > 0;
    public bool IsVisible => IsAvailable && (HasItems || HasEditor || HasNotice);
    public bool IsBusy => _catalog.SelectedTask is { } task && (_actions.IsPreparing(task.Id) || IsPreparing(task.Id));
    public bool HasNotice => _notice is not null;
    public string Notice => _notice is null ? string.Empty : WorkspaceText.Get(_notice);
    public string Title => string.Format(WorkspaceText.Get("FollowupTitle"), Items.Count);
    public string EnqueueLabel => WorkspaceText.Get("FollowupEnqueue");
    public string ContinueLabel => WorkspaceText.Get("FollowupContinue");
    public string RefreshLabel => WorkspaceText.Get("FollowupRefresh");
    public string SaveLabel => WorkspaceText.Get("FollowupSave");
    public string CancelLabel => WorkspaceText.Get("FollowupCancelEdit");
    public string Hint => WorkspaceText.Get("FollowupHint");
    public IAsyncRelayCommand EnqueueCommand { get; }
    public IAsyncRelayCommand ContinueCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveEditCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand SubmitCommand { get; }
    public bool IsPreparing(Guid taskId) => _runtime?.IsPreparing(taskId) == true;
    public void CancelPreparation(Guid taskId) => _runtime?.CancelPreparation(taskId);

    private bool CanEnqueue() => !_disposed && IsAvailable && !IsBusy && (_queue?.Items.Count ?? 0) < ConversationFollowupQueue.MaximumItems && _canEnqueue();
    private bool CanContinue() => !_disposed && IsAvailable && _queue?.Items.Count > 0 && !IsBusy && _canContinue();
    private void SelectTask()
    {
        _generation++; _queue = null; Items.Clear(); _notice = null;
        NotifyStateChanged(); _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_disposed || _actions.IsStopping || _runtime is null || _catalog.SelectedTask is not { } task) return;
        var generation = _generation;
        await _actions.RunAsync(task.Id, false, async token =>
        {
            try
            {
                var queue = await _runtime.LoadAsync(task.Id, token);
                await AvaloniaUiThread.RunAsync(() =>
                {
                    if (!_disposed && generation == _generation && _catalog.SelectedTask?.Id == task.Id)
                        Apply(queue, _runtime.Notice(task.Id));
                }, token);
            }
            catch (Exception exception)
            {
                await AvaloniaUiThread.RunAsync(() =>
                {
                    if (!_disposed && generation == _generation) { _notice = WorkspaceFollowupRuntime.FailureKey(exception); NotifyStateChanged(); }
                }, CancellationToken.None);
            }
        });
    }

    private void HandleChanged(Guid taskId, ConversationFollowupQueue? queue, string? notice)
    {
        if (_disposed || _catalog.SelectedTask?.Id != taskId) return;
        if (queue is not null) Apply(queue, notice);
        else { _notice = notice; NotifyStateChanged(); _ = RefreshAsync(); }
    }

    private void Apply(ConversationFollowupQueue queue, string? notice)
    {
        if (_queue?.TaskId == queue.TaskId && _queue.Revision > queue.Revision) return;
        _queue = queue; _notice = notice ?? (queue.Items.Count > 0 && _runtime?.IsArmed(queue.TaskId) != true ? "FollowupPaused" : null); Items.Clear();
        foreach (var item in queue.Items)
            Items.Add(new(queue, item, BeginEdit, delta => MoveAsync(queue, item.Id, delta), () => RemoveAsync(queue, item.Id),
                () => CanMutate(queue), () => CanMutate(queue) && Editor is null));
        NotifyStateChanged();
    }

    private bool CanMutate(ConversationFollowupQueue queue) => !_disposed && !IsBusy
        && _catalog.SelectedTask?.Id == queue.TaskId && _queue?.Revision == queue.Revision;

    private void BeginEdit(WorkspaceFollowupRowViewModel row)
    {
        if (_disposed || IsBusy || _catalog.SelectedTask?.Id != row.Expected.TaskId) return;
        if (!_editors.ContainsKey(row.Expected.TaskId)) _editors[row.Expected.TaskId] = row;
        NotifyStateChanged();
    }

    private void CancelEdit()
    {
        if (_catalog.SelectedTask is { } task) _editors.Remove(task.Id);
        NotifyStateChanged();
    }

    private Task SaveEditAsync()
    {
        var editor = Editor!;
        var text = editor.EditText.Trim();
        if (text.Length > ConversationDraft.MaximumTextLength || text.Length == 0 && !editor.Item.Attachments.Any(item => item.MediaType != "text/plain"))
        { _notice = "FollowupInvalidPrompt"; NotifyStateChanged(); return Task.CompletedTask; }
        var changed = editor.Expected.Items.Select(item => item.Id == editor.Id ? item with { Prompt = text } : item).ToArray();
        return ActAsync(async () =>
        {
            await _runtime!.ReplaceAsync(editor.Expected, changed);
            await AvaloniaUiThread.RunAsync(() =>
            { _editors.Remove(editor.Expected.TaskId); NotifyStateChanged(); }, CancellationToken.None);
        });
    }

    private Task RemoveAsync(ConversationFollowupQueue queue, Guid id) => _catalog.SelectedTask?.Id != queue.TaskId
        ? Task.CompletedTask : ActAsync(() => _runtime!.ReplaceAsync(queue, queue.Items.Where(item => item.Id != id).ToArray()));

    private Task MoveAsync(ConversationFollowupQueue queue, Guid id, int delta)
    {
        if (_catalog.SelectedTask?.Id != queue.TaskId) return Task.CompletedTask;
        var items = queue.Items.ToArray();
        var index = Array.FindIndex(items, item => item.Id == id);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= items.Length) return Task.CompletedTask;
        (items[index], items[target]) = (items[target], items[index]);
        return ActAsync(() => _runtime!.ReplaceAsync(queue, items));
    }

    private Task ActAsync(Func<Task> action)
    {
        if (_disposed || _actions.IsStopping || _runtime is null || _catalog.SelectedTask is not { } task) return Task.CompletedTask;
        return _actions.RunAsync(task.Id, true, async token =>
        {
            try { token.ThrowIfCancellationRequested(); await action(); }
            catch (Exception exception)
            {
                await AvaloniaUiThread.RunAsync(() =>
                {
                    if (!_disposed && _catalog.SelectedTask?.Id == task.Id)
                    { _notice = WorkspaceFollowupRuntime.FailureKey(exception); NotifyStateChanged(); }
                }, CancellationToken.None);
            }
        });
    }

    internal void NotifyStateChanged()
    {
        if (_disposed) return;
        foreach (var name in new[] { nameof(Editor), nameof(HasEditor), nameof(HasItems), nameof(IsVisible), nameof(IsBusy), nameof(HasNotice), nameof(Notice), nameof(Title), nameof(CanEnqueueMessage) }) OnPropertyChanged(name);
        EnqueueCommand.NotifyCanExecuteChanged(); ContinueCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged();
        SaveEditCommand.NotifyCanExecuteChanged(); CancelEditCommand.NotifyCanExecuteChanged(); SubmitCommand.NotifyCanExecuteChanged();
        foreach (var row in Items) row.NotifyCommands();
    }

    private Task HandleStateChangedAsync() => AvaloniaUiThread.RunAsync(() => { NotifyStateChanged(); StateChanged?.Invoke(); }, CancellationToken.None);
    private void HandlePrimaryChanged(object? sender, EventArgs args) => NotifyStateChanged();
    private void HandleRuntimeStateChanged() { NotifyStateChanged(); StateChanged?.Invoke(); }
    public Task StopAsync() => Task.WhenAll(_runtime?.StopAsync() ?? Task.CompletedTask, _actions.StopAsync());
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _generation++;
        _catalog.SelectionChanged -= SelectTask; _actions.StateChanged -= HandleStateChangedAsync;
        _primary.CanExecuteChanged -= HandlePrimaryChanged;
        if (_runtime is not null) { _runtime.Changed -= HandleChanged; _runtime.StateChanged -= HandleRuntimeStateChanged; _runtime.Dispose(); }
        _actions.Dispose();
    }
}
