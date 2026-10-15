using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;
using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFileChangesViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly WorkspaceTaskPanelCoordinator _panels;
    private readonly Dictionary<ChatMessageViewModel, bool> _observed = [];
    private IReadOnlyList<WorkspaceFileChangeViewModel> _visibleFiles = [];
    private bool _expanded;
    private bool _incomplete;
    private bool _disposed;

    public WorkspaceFileChangesViewModel(WorkspaceCatalogViewModel catalog, WorkspaceTaskPanelCoordinator panels,
        WorkspaceFileChangeUndoService? undoService = null, WorkspaceFileChangeStatisticsService? statisticsService = null)
    {
        _catalog = catalog;
        _panels = panels;
        ReviewCommand = new GuardedAsyncRelayCommand(ReviewAsync, () => !_disposed && Files.Count > 0 && _catalog.SelectedTask is not null);
        OpenFileCommand = new GuardedAsyncRelayCommand<string>(OpenFileAsync,
            path => !_disposed && _catalog.SelectedTask is not null && Files.Any(file => file.Path == path));
        ExpandCommand = new RelayCommand(Expand, () => !_disposed && HasMoreFiles);
        Undo = new(catalog, panels, this, undoService);
        Statistics = new(statisticsService);
        Undo.PropertyChanged += HandleUndoChanged;
        catalog.Messages.CollectionChanged += HandleMessagesChanged;
        catalog.SelectionChanged += HandleSelectionChanged;
        ObserveMessages(catalog.Messages);
        Refresh(true);
    }

    public event Action? ReviewRequested;
    public ObservableCollection<WorkspaceFileChangeViewModel> Files { get; } = [];
    public IReadOnlyList<WorkspaceFileChangeViewModel> VisibleFiles => _visibleFiles;
    public GuardedAsyncRelayCommand ReviewCommand { get; }
    public IRelayCommand ExpandCommand { get; }
    public GuardedAsyncRelayCommand<string> OpenFileCommand { get; }
    public WorkspaceFileChangeUndoViewModel Undo { get; }
    public WorkspaceFileChangeStatisticsViewModel Statistics { get; }
    public bool HasChanges => Files.Count > 0 || _incomplete || Undo.HasNotice;
    public bool HasMoreFiles => !_expanded && Files.Count > 3;
    public bool IsIncomplete => _incomplete;
    public string Title => string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("FileChangesTitle"), Files.Count);
    public string ScopeLabel => WorkspaceText.Get("FileChangesScope");
    public string ScopeHint => WorkspaceText.Get("FileChangesScopeHint");
    public string ReviewLabel => WorkspaceText.Get("FileChangesReview");
    public string UndoLabel => WorkspaceText.Get("FileChangesUndo");
    public string UndoHint => WorkspaceText.Get("FileChangesUndoUnavailable");
    public string IncompleteHint => WorkspaceText.Get("FileChangesIncomplete");
    public string ExpandLabel => string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("FileChangesExpand"), Math.Max(0, Files.Count - 3));

    private void HandleMessagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        var changed = args.Action == NotifyCollectionChangedAction.Reset;
        if (changed)
        {
            foreach (var message in _observed.Keys) message.PropertyChanged -= HandleMessageChanged;
            _observed.Clear();
            ObserveMessages(_catalog.Messages);
        }
        else
        {
            foreach (ChatMessageViewModel message in args.OldItems ?? Array.Empty<ChatMessageViewModel>())
            {
                changed |= HasRecordedChanges(message.Tool);
                if (_catalog.Messages.Contains(message)) continue;
                message.PropertyChanged -= HandleMessageChanged;
                _observed.Remove(message);
            }
            if (args.NewItems is not null) changed |= ObserveMessages(args.NewItems.Cast<ChatMessageViewModel>());
        }
        if (changed) Refresh();
    }

    private void HandleSelectionChanged()
    {
        ReviewCommand.Cancel();
        OpenFileCommand.Cancel();
        _expanded = false;
        Refresh(true);
    }

    private void HandleMessageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ChatMessageViewModel.Tool)
            || sender is not ChatMessageViewModel message || !_observed.TryGetValue(message, out var hadChanges)) return;
        var hasChanges = HasRecordedChanges(message.Tool);
        _observed[message] = hasChanges;
        if (hadChanges || hasChanges) Refresh();
    }

    private static bool HasRecordedChanges(ToolConversationPayload? payload) =>
        payload is { FileChanges.Count: > 0 } or { FileChangesTruncated: true };

    private bool ObserveMessages(IEnumerable<ChatMessageViewModel> messages)
    {
        var changed = false;
        foreach (var message in messages)
        {
            var hasChanges = HasRecordedChanges(message.Tool);
            changed |= hasChanges;
            if (_observed.TryAdd(message, hasChanges)) message.PropertyChanged += HandleMessageChanged;
        }
        return changed;
    }

    private void Refresh(bool force = false)
    {
        if (_disposed) return;
        var changes = new Dictionary<string, ToolFileChange>(StringComparer.OrdinalIgnoreCase);
        var incomplete = false;
        foreach (var message in _catalog.Messages)
        {
            if (message.Tool is not { } payload) continue;
            incomplete |= payload.FileChangesTruncated;
            foreach (var change in payload.FileChanges ?? [])
            {
                if (change is null || !change.IsValid) { incomplete = true; continue; }
                if (changes.TryGetValue(change.Path, out var previous))
                {
                    if (previous == change) continue;
                    if (string.Equals(previous.AfterSha256, change.BeforeSha256, StringComparison.OrdinalIgnoreCase))
                        changes[change.Path] = change with { BeforeSha256 = previous.BeforeSha256, BeforeSnapshot = previous.BeforeSnapshot };
                    else { changes[change.Path] = change; incomplete = true; }
                }
                else changes[change.Path] = change;
            }
        }
        var root = _catalog.SelectedTask?.WorktreePath ?? _catalog.SelectedProject?.PrimaryDirectory;
        var changed = PublishFiles(changes.Values.Where(change =>
            !string.Equals(change.BeforeSha256, change.AfterSha256, StringComparison.OrdinalIgnoreCase)), root);
        changed |= _incomplete != incomplete;
        _incomplete = incomplete;
        if (changed || force) NotifyState();
    }

    private bool PublishFiles(IEnumerable<ToolFileChange> changes, string? root)
    {
        var index = 0;
        var changed = false;
        foreach (var change in changes)
        {
            var displayPath = DisplayPath(root, change.Path);
            if (index >= Files.Count)
            {
                Files.Add(new(change, displayPath));
                changed = true;
            }
            else if (Files[index].Change != change || Files[index].DisplayPath != displayPath)
            {
                Files[index] = new(change, displayPath);
                changed = true;
            }
            index++;
        }
        while (Files.Count > index) { Files.RemoveAt(Files.Count - 1); changed = true; }
        return changed;
    }

    private static string DisplayPath(string? root, string path)
    {
        try { return root is null ? path : Path.GetRelativePath(root, path).Replace('\\', '/'); }
        catch (ArgumentException) { return path; }
    }

    private void NotifyState()
    {
        var context = Statistics.IsEnabled && _catalog.SelectedTask is { } task && _catalog.SelectedProject is { } project
            ? _panels.CreateContext(task, project) : null;
        Statistics.Update(Files, context, _incomplete);
        _visibleFiles = _expanded ? Files : Files.Take(3).ToArray();
        OnPropertyChanged(nameof(VisibleFiles));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasMoreFiles));
        OnPropertyChanged(nameof(IsIncomplete));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ExpandLabel));
        ReviewCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();
        ExpandCommand.NotifyCanExecuteChanged();
    }

    private void Expand() { _expanded = true; NotifyState(); }

    public async Task StopAsync()
    {
        try { await Statistics.StopAsync(); }
        finally { await Undo.StopAsync(); }
    }

    private void HandleUndoChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceFileChangeUndoViewModel.HasNotice)) OnPropertyChanged(nameof(HasChanges));
    }

    private async Task ReviewAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _catalog.SelectedTask is not { } task) return;
        ReviewRequested?.Invoke();
        foreach (var file in Files.Take(FilePreviewPaneViewModel.MaximumTabs).ToArray())
        {
            if (_disposed || _catalog.SelectedTask?.Id != task.Id || cancellationToken.IsCancellationRequested) return;
            await _panels.Previews.OpenChangeAsync(file.Change, cancellationToken);
        }
    }

    private Task OpenFileAsync(string? path, CancellationToken cancellationToken) =>
        Files.FirstOrDefault(file => file.Path == path) is { } file
            ? _panels.Previews.OpenChangeAsync(file.Change, cancellationToken) : Task.CompletedTask;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.Messages.CollectionChanged -= HandleMessagesChanged;
        _catalog.SelectionChanged -= HandleSelectionChanged;
        foreach (var message in _observed.Keys) message.PropertyChanged -= HandleMessageChanged;
        _observed.Clear();
        Undo.PropertyChanged -= HandleUndoChanged;
        Undo.Dispose();
        Statistics.Dispose();
        ReviewCommand.Dispose();
        OpenFileCommand.Dispose();
        ExpandCommand.NotifyCanExecuteChanged();
        ReviewRequested = null;
    }
}
