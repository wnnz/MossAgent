using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>Composer skill picker: lists the skill components of enabled plugins and lets one
/// conversation deviate from the global per-component choices. The effective state resolves
/// task row → global row → default-enabled. While the composer has no task yet, toggles stay
/// in an in-memory draft; <see cref="RememberTask"/> binds that draft to the freshly created
/// task — the same moment <see cref="WorkspaceConversationPreferences.Remember"/> runs — and a
/// serialized drain flushes it through <see cref="ITaskSkillOverrides"/>. Failed flush entries
/// stay queued and surface an error; the user's choice is never silently dropped.</summary>
public sealed class WorkspaceSkillPickerViewModel : ObservableObject, IDisposable
{
    private readonly IPluginPackageInstaller _installer;
    private readonly IPluginComponentSettings? _componentSettings;
    private readonly ITaskSkillOverrides _overrides;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly SettingsOperationLifetime _operations;
    private readonly Dictionary<(Guid PluginId, string Path), bool> _draft = [];
    private readonly Dictionary<(Guid TaskId, Guid PluginId, string Path), bool> _pending = [];
    private readonly object _readLock = new();
    private CancellationTokenSource? _readCancellation;
    private Task? _writeTask;
    private Guid? _taskId;
    private int _generation;
    private bool _isLoading;
    private bool _hasLoadError;
    private string? _status;
    private bool _statusIsError;
    private bool _isPickerOpen;
    private bool _stopping;
    private bool _disposed;

    public WorkspaceSkillPickerViewModel(IPluginPackageInstaller installer,
        IPluginComponentSettings? componentSettings, ITaskSkillOverrides overrides,
        WorkspaceCatalogViewModel catalog, TimeSpan? operationTimeout = null)
    {
        _installer = installer;
        _componentSettings = componentSettings;
        _overrides = overrides;
        _catalog = catalog;
        _operations = new SettingsOperationLifetime(operationTimeout);
        ToggleSkillCommand = new GuardedAsyncRelayCommand<WorkspaceSkillPickerRowViewModel>(
            ToggleSkillAsync, row => CanOperate && row is not null);
        ResetCommand = new GuardedAsyncRelayCommand(ResetAsync, () => CanOperate && HasSkills);
        RefreshCommand = new GuardedAsyncRelayCommand(token => LoadCoreAsync(token), () => CanOperate);
        foreach (var command in Operations) command.PropertyChanged += HandleOperationChanged;
        catalog.SelectionChanged += SelectContext;
        SelectContext();
        _ = LoadCoreAsync(CancellationToken.None);
    }

    public ObservableCollection<WorkspaceSkillPickerGroupViewModel> Groups { get; } = [];
    public IAsyncRelayCommand<WorkspaceSkillPickerRowViewModel> ToggleSkillCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public string Label => WorkspaceText.Get("WorkspaceComposerSkills");
    public string ResetLabel => WorkspaceText.Get("WorkspaceSkillReset");
    public string EmptyHint => WorkspaceText.Get("WorkspaceSkillEmpty");
    public int TotalSkills => Groups.Sum(group => group.Items.Count);
    public int EnabledSkills => Groups.Sum(group => group.Items.Count(row => row.IsEnabled));
    public bool HasSkills => TotalSkills > 0;
    public bool ShowEmptyHint => !_isLoading && !HasLoadError && !HasSkills;
    public string Summary => HasSkills ? $"{EnabledSkills}/{TotalSkills}" : string.Empty;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string? Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }
    public bool StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }
    public bool IsLoading => _isLoading;
    public bool HasLoadError => _hasLoadError;
    public bool CanOperate => !_disposed && !_isLoading && !ToggleSkillCommand.IsRunning && !ResetCommand.IsRunning;
    internal int PendingDraftCount => _draft.Count;

    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set
        {
            if (SetProperty(ref _isPickerOpen, value) && value && !_disposed && !_stopping)
                _ = LoadCoreAsync(CancellationToken.None);
        }
    }

    /// <summary>Binds the unbound-conversation draft to the task the interactive send just
    /// created. Calls made while an existing task is bound are ignored — the draft belongs to
    /// the not-yet-persisted conversation only.</summary>
    public void RememberTask(Guid taskId)
    {
        if (_disposed || _stopping || _taskId is not null || _draft.Count == 0) return;
        foreach (var (key, enabled) in _draft)
            _pending[(taskId, key.PluginId, key.Path)] = enabled;
        _draft.Clear();
        KickDrain();
    }

    public Task FlushAsync() => _writeTask ?? Task.CompletedTask;

    public Task ReloadAsync() => LoadCoreAsync(CancellationToken.None);

    public async Task StopAsync()
    {
        _stopping = true;
        _generation++;
        CancelRead();
        await FlushAsync();
    }

    private IAsyncRelayCommand[] Operations => [ToggleSkillCommand, ResetCommand, RefreshCommand];

    private void SelectContext()
    {
        if (_disposed || _stopping) return;
        var taskId = _catalog.SelectedTask?.Id;
        if (_taskId == taskId) return;
        _taskId = taskId;
        _ = LoadCoreAsync(CancellationToken.None);
    }

    private async Task ToggleSkillAsync(WorkspaceSkillPickerRowViewModel? row, CancellationToken cancellationToken)
    {
        if (_disposed || row is null) return;
        var enabled = row.IsEnabled;
        using var operation = _operations.CreateOperation(cancellationToken);
        try
        {
            if (_taskId is { } taskId)
                await _overrides.SetSkillEnabledAsync(taskId, row.PluginId, row.RelativePath, enabled, operation.Token);
            else
                _draft[(row.PluginId, row.RelativePath)] = enabled;
            SetStatus(null);
        }
        catch (Exception)
        {
            row.IsEnabled = !enabled;
            SetStatus(WorkspaceText.Get("WorkspaceSkillSaveFailed"), isError: true);
        }
        NotifySummaryChanged();
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        using var operation = _operations.CreateOperation(cancellationToken);
        try
        {
            if (_taskId is { } taskId)
            {
                foreach (var key in _pending.Keys.Where(key => key.TaskId == taskId).ToList())
                    _pending.Remove(key);
                await FlushAsync();
                await _overrides.ClearForTaskAsync(taskId, operation.Token);
            }
            else
            {
                _draft.Clear();
            }
            SetStatus(null);
        }
        catch (Exception)
        {
            SetStatus(WorkspaceText.Get("WorkspaceSkillSaveFailed"), isError: true);
        }
        await LoadCoreAsync(CancellationToken.None);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _stopping) return;
        var generation = ++_generation;
        CancelRead();
        var read = _operations.CreateOperation(cancellationToken);
        lock (_readLock) _readCancellation = read;
        _isLoading = true;
        _hasLoadError = false;
        NotifyCatalogState();
        try
        {
            // The drain removes pending keys before each write, so key presence cannot gate
            // this wait — always let an in-flight draft flush finish before reading task rows.
            var flush = _writeTask;
            if (flush is not null) await flush;
            var plugins = await _installer.ListAsync(read.Token);
            var taskChoices = await TaskChoicesAsync(read.Token);
            var groups = await Task.WhenAll(plugins
                .Where(plugin => plugin.State == InstalledPluginState.Enabled)
                .OrderBy(plugin => plugin.Name, StringComparer.Ordinal)
                .Select(plugin => BuildGroupAsync(plugin, taskChoices, read.Token)));
            if (_disposed || _stopping || read.IsCancellationRequested || generation != _generation) return;
            Groups.Clear();
            foreach (var group in groups.Where(group => group.Items.Count > 0)) Groups.Add(group);
            _isLoading = false;
            NotifyCatalogState();
        }
        catch (Exception)
        {
            if (_disposed || _stopping || generation != _generation) return;
            _hasLoadError = true;
            SetStatus(WorkspaceText.Get("WorkspaceSkillLoadFailed"), isError: true);
        }
        finally
        {
            lock (_readLock)
            {
                if (ReferenceEquals(_readCancellation, read)) _readCancellation = null;
                read.Dispose();
            }
            if (!_disposed && generation == _generation)
            {
                _isLoading = false;
                NotifyCatalogState();
            }
        }
    }

    private async Task<IReadOnlyDictionary<(Guid PluginId, string Path), bool>> TaskChoicesAsync(
        CancellationToken token)
    {
        if (_taskId is not { } taskId) return _draft;
        var rows = await _overrides.ListForTaskAsync(taskId, token);
        token.ThrowIfCancellationRequested();
        return rows.ToDictionary(row => (row.PluginId, row.ComponentPath), row => row.Enabled);
    }

    private async Task<WorkspaceSkillPickerGroupViewModel> BuildGroupAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<(Guid PluginId, string Path), bool> taskChoices, CancellationToken token)
    {
        var components = plugin.Components
            .Where(component => component.Kind == PluginComponentKind.Skill)
            .OrderBy(component => component.RelativePath, StringComparer.Ordinal).ToList();
        if (components.Count == 0) return new WorkspaceSkillPickerGroupViewModel(plugin, []);
        var global = _componentSettings is null ? null
            : await _componentSettings.ListOverridesAsync(plugin.Id, token);
        token.ThrowIfCancellationRequested();
        return new WorkspaceSkillPickerGroupViewModel(plugin,
            [.. components.Select(component => new WorkspaceSkillPickerRowViewModel(plugin.Id, component,
                Effective(plugin.Id, component.RelativePath, taskChoices, global)))]);
    }

    private static bool Effective(Guid pluginId, string path,
        IReadOnlyDictionary<(Guid PluginId, string Path), bool> taskChoices,
        IReadOnlyDictionary<string, bool>? global) =>
        taskChoices.TryGetValue((pluginId, path), out var taskEnabled) ? taskEnabled
            : global is not null && global.TryGetValue(path, out var stored) ? stored : true;

    private void KickDrain()
    {
        if (_writeTask?.IsCompleted != false) _writeTask = DrainWritesAsync();
    }

    private async Task DrainWritesAsync()
    {
        List<KeyValuePair<(Guid TaskId, Guid PluginId, string Path), bool>>? failed = null;
        while (!_disposed && _pending.Count > 0)
        {
            var (key, enabled) = _pending.First();
            _pending.Remove(key);
            using var operation = _operations.CreateOperation(_operations.Token);
            try
            {
                await _overrides.SetSkillEnabledAsync(key.TaskId, key.PluginId, key.Path, enabled,
                    operation.Token);
            }
            catch (Exception)
            {
                (failed ??= []).Add(new KeyValuePair<(Guid, Guid, string), bool>(key, enabled));
            }
        }
        if (failed is null) return;
        foreach (var (key, enabled) in failed) _pending[key] = enabled;
        if (!_disposed && !_stopping)
            SetStatus(WorkspaceText.Get("WorkspaceSkillSaveFailed"), isError: true);
    }

    private void CancelRead()
    {
        lock (_readLock) _readCancellation?.Cancel();
    }

    private void SetStatus(string? message, bool isError = false)
    {
        if (_disposed) return;
        Status = message;
        StatusIsError = isError && message is not null;
    }

    private void NotifySummaryChanged()
    {
        foreach (var property in new[] { nameof(TotalSkills), nameof(EnabledSkills), nameof(HasSkills),
            nameof(ShowEmptyHint), nameof(Summary) })
            OnPropertyChanged(property);
        ResetCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCatalogState()
    {
        foreach (var property in new[] { nameof(IsLoading), nameof(HasLoadError), nameof(CanOperate) })
            OnPropertyChanged(property);
        NotifySummaryChanged();
        foreach (var command in Operations) command.NotifyCanExecuteChanged();
    }

    private void HandleOperationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || args.PropertyName != nameof(IAsyncRelayCommand.IsRunning)) return;
        OnPropertyChanged(nameof(CanOperate));
        foreach (var command in Operations) command.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.SelectionChanged -= SelectContext;
        CancelRead();
        foreach (var command in Operations)
        {
            command.PropertyChanged -= HandleOperationChanged;
            ((IDisposable)command).Dispose();
        }
        _operations.Dispose();
    }
}
