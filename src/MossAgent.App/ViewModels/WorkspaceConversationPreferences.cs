using System.ComponentModel;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

public sealed class WorkspaceConversationPreferences : IDisposable
{
    private readonly IConversationPreferenceRepository _repository;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly ModelPickerViewModel _models;
    private readonly WorkspaceAgentPickerViewModel _agents;
    private readonly SettingsOperationLifetime _operations;
    private readonly Dictionary<Guid, ConversationModelPreference?> _cache = [];
    private readonly Dictionary<Guid, ConversationModelPreference> _pending = [];
    private readonly HashSet<Guid> _failedWrites = [];
    private CancellationTokenSource? _readCancellation;
    private Task? _writeTask;
    private Guid? _taskId;
    private ConversationModelPreference? _currentPreference;
    private ConversationModelPreference? _draftPreference;
    private int _generation;
    private bool _loaded = true;
    private bool _applied = true;
    private bool _restoring;
    private bool _stopping;
    private bool _disposed;

    public WorkspaceConversationPreferences(IConversationPreferenceRepository repository,
        WorkspaceCatalogViewModel catalog, ModelPickerViewModel models, WorkspaceAgentPickerViewModel agents,
        TimeSpan? operationTimeout = null)
    {
        _repository = repository;
        _catalog = catalog;
        _models = models;
        _agents = agents;
        _operations = new SettingsOperationLifetime(operationTimeout);
        catalog.SelectionChanged += SelectContext;
        models.PreferenceChanged += CaptureChange;
        agents.PreferenceChanged += CaptureChange;
        models.PropertyChanged += HandleReadiness;
        agents.PropertyChanged += HandleReadiness;
        models.RefreshConversationRequested += RefreshAsync;
        SelectContext();
    }

    public ConversationModelPreference? Capture()
    {
        if (!_models.CanUseSelectedModel || _models.SelectedItem is not { } item) return null;
        var followAgent = _agents.PreferredProfileId is not null && _agents.FollowsAgentReasoning;
        return new(item.Provider.Id, item.Model.Id,
            followAgent || _models.SelectedReasoning == UiText.T("默认") ? null : _models.EffectiveReasoningEffort,
            _agents.PreferredProfileId, followAgent);
    }

    public void Remember(Guid taskId, ConversationModelPreference? preference)
    {
        if (_disposed || _stopping || preference is null) return;
        _cache[taskId] = preference;
        _pending[taskId] = preference;
        if (_writeTask?.IsCompleted != false) _writeTask = DrainWritesAsync();
    }

    public Task FlushAsync() => _writeTask ?? Task.CompletedTask;

    public async Task StopAsync()
    {
        _stopping = true;
        _generation++;
        _readCancellation?.Cancel();
        await FlushAsync();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _stopping) return;
        if (_taskId is { } taskId && _failedWrites.Contains(taskId))
        {
            Remember(taskId, _cache.GetValueOrDefault(taskId));
            await FlushAsync();
            if (_failedWrites.Contains(taskId)) return;
        }
        await ReadAsync(_taskId, cancellationToken);
    }

    internal void ReapplySelection()
    {
        if (_disposed || _stopping || !_loaded) return;
        _applied = false;
        ApplyIfReady();
    }

    private void SelectContext()
    {
        if (_disposed || _stopping) return;
        var taskId = _catalog.SelectedTask?.Id;
        if (_taskId == taskId) return;
        if (_taskId is null) _draftPreference = Capture();
        _taskId = taskId;
        _ = ReadAsync(taskId);
    }

    private async Task ReadAsync(Guid? taskId, CancellationToken cancellationToken = default)
    {
        var generation = ++_generation;
        _readCancellation?.Cancel();
        using var read = _operations.CreateOperation(cancellationToken);
        _readCancellation = read;
        _loaded = false;
        _applied = false;
        _models.SetConversationState(true, "ConversationPreferenceLoading");
        try
        {
            var preference = taskId is null ? _draftPreference
                : _cache.TryGetValue(taskId.Value, out var cached) ? cached
                : await _repository.GetAsync(taskId.Value, read.Token).WaitAsync(read.Token);
            if (_disposed || _stopping || read.IsCancellationRequested || generation != _generation) return;
            _currentPreference = preference;
            if (taskId is { } id) _cache[id] = preference;
            _loaded = true;
            ApplyIfReady();
        }
        catch (Exception)
        {
            if (!_disposed && !_stopping && generation == _generation)
                _models.SetConversationState(true, "ConversationPreferenceLoadFailed");
        }
        finally
        {
            if (ReferenceEquals(_readCancellation, read)) _readCancellation = null;
        }
    }

    private void ApplyIfReady()
    {
        if (_disposed || _stopping || _restoring || !_loaded || _applied || !_models.CanChooseModel || !_agents.CanChoose) return;
        _restoring = true;
        try
        {
            var agent = _currentPreference?.AgentProfileId is { } id
                ? _agents.Items.FirstOrDefault(profile => profile.Id == id) : null;
            _agents.RestoreConversation(_currentPreference?.AgentProfileId, _currentPreference?.FollowAgentReasoning ?? true);
            var preference = agent is null || _currentPreference is null ? _currentPreference : _currentPreference with
            {
                ProviderId = agent.ProviderId,
                ModelProfileId = agent.ModelProfileId,
                ReasoningEffort = _currentPreference.FollowAgentReasoning ? agent.ReasoningEffort : _currentPreference.ReasoningEffort
            };
            _models.RestoreConversation(preference);
            _currentPreference ??= Capture();
            if (_taskId is { } selectedTaskId) _cache[selectedTaskId] = _currentPreference;
            var missingAgent = _currentPreference?.AgentProfileId is not null && agent is null;
            _applied = true;
            _models.SetConversationState(missingAgent, missingAgent ? "ConversationPreferenceAgentUnavailable"
                : _taskId is { } taskId && _failedWrites.Contains(taskId) ? "ConversationPreferenceSaveFailed" : null);
        }
        finally { _restoring = false; }
    }

    private void HandleReadiness(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ModelPickerViewModel.CanChooseModel) or nameof(WorkspaceAgentPickerViewModel.CanChoose))
            ApplyIfReady();
    }

    private void CaptureChange()
    {
        if (_disposed || _stopping || _restoring || Capture() is not { } preference) return;
        _generation++;
        _readCancellation?.Cancel();
        _currentPreference = preference;
        _loaded = true;
        _applied = true;
        _models.SetConversationState(false, null);
        if (_taskId is { } id) Remember(id, preference);
        else _draftPreference = preference;
    }

    private async Task DrainWritesAsync()
    {
        while (!_disposed && _pending.Count > 0)
        {
            var (taskId, preference) = _pending.First();
            _pending.Remove(taskId);
            using var operation = _operations.CreateOperation(_operations.Token);
            try
            {
                if (!await _repository.SaveAsync(taskId, preference, operation.Token)) throw new InvalidOperationException();
                _failedWrites.Remove(taskId);
            }
            catch (Exception)
            {
                _failedWrites.Add(taskId);
            }
            PublishWriteNotice(taskId, preference);
        }
    }

    private void PublishWriteNotice(Guid taskId, ConversationModelPreference preference)
    {
        if (_disposed || _stopping || !_loaded || !_applied || _taskId != taskId || _pending.ContainsKey(taskId)
            || _cache.GetValueOrDefault(taskId) != preference) return;
        var unavailable = _agents.IsSelectedUnavailable;
        _models.SetConversationState(unavailable, unavailable ? "ConversationPreferenceAgentUnavailable"
            : _failedWrites.Contains(taskId) ? "ConversationPreferenceSaveFailed" : null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operations.Dispose();
        _catalog.SelectionChanged -= SelectContext;
        _models.PreferenceChanged -= CaptureChange;
        _agents.PreferenceChanged -= CaptureChange;
        _models.PropertyChanged -= HandleReadiness;
        _agents.PropertyChanged -= HandleReadiness;
        _models.RefreshConversationRequested -= RefreshAsync;
    }
}

