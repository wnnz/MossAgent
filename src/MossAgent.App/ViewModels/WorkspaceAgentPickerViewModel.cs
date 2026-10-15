using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAgentPickerViewModel : ObservableObject, IDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(WorkspaceAgentPickerViewModel).Assembly);
    private readonly IAgentConfigurationRepository _repository;
    private readonly ModelPickerViewModel _models;
    private readonly SettingsOperationLifetime _operations;
    private CancellationTokenSource? _readCancellation;
    private AgentProfile? _selectedProfile;
    private Guid? _preferredProfileId;
    private bool _followAgentReasoning = true;
    private int _generation;
    private bool _catalogAvailable;
    private bool _isLoading;
    private bool _hasLoadError;
    private bool _synchronizingModel;
    private bool _disposed;

    public WorkspaceAgentPickerViewModel(IAgentConfigurationRepository repository, ModelPickerViewModel models)
        : this(repository, models, TimeSpan.FromSeconds(30)) { }

    internal WorkspaceAgentPickerViewModel(IAgentConfigurationRepository repository,
        ModelPickerViewModel models, TimeSpan readTimeout)
    {
        _repository = repository;
        _models = models;
        _operations = new SettingsOperationLifetime(readTimeout);
        SelectCommand = new RelayCommand<AgentProfile?>(Select, CanSelect);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, () => !_disposed && !IsLoading && !_models.IsLoading);
        _models.ExplicitSelectionChanged += ClearForExplicitModelChoice;
        _models.ModelSelected += HandleModelSelection;
        _models.PropertyChanged += HandleModelStateChanged;
        _models.PreferenceChanged += HandleReasoningChoice;
    }

    public ObservableCollection<AgentProfile> Items { get; } = [];
    public AgentProfile? SelectedProfile => _selectedProfile;
    public Guid? PreferredProfileId => _preferredProfileId;
    internal bool FollowsAgentReasoning => _followAgentReasoning;
    public bool HasAgents => Items.Count > 0 || PreferredProfileId is not null;
    public bool IsLoading => _isLoading;
    public bool HasLoadError => _hasLoadError;
    public bool CanChoose => !_disposed && _catalogAvailable && !IsLoading && !_models.IsLoading;
    public bool IsSelectedUnavailable => CanChoose && PreferredProfileId is not null
        && (SelectedProfile is not { } profile || !ReferenceEquals(profile, Find(profile.Id)) || !HasMatchingModel(profile));
    public bool CanUseSelection => CanChoose && !HasLoadError
        && !IsSelectedUnavailable;
    public bool HasCatalogNotice => !_disposed && (IsLoading || HasLoadError
        || PreferredProfileId is not null && (_models.HasLoadError || _models.IsLoading || IsSelectedUnavailable));
    public string SelectedLabel => SelectedProfile is not { } profile
        ? GetLabel(PreferredProfileId is null ? "DefaultAgentLabel" : "UnavailableAgentLabel")
        : IsSelectedUnavailable ? string.Format(GetLabel("UnavailableAgentLabelFormat"), profile.Name) : profile.Name;
    public string CatalogNotice => GetLabel(IsLoading ? "LoadingAgents"
        : HasLoadError ? "AgentCatalogReloadFailed"
        : _models.IsLoading ? "LoadingAgentModels"
        : _models.HasLoadError ? "AgentModelsReloadFailed" : "SelectedAgentUnavailable");
    public string RefreshLabel => GetLabel("RefreshAgentConfiguration");
    public IRelayCommand<AgentProfile?> SelectCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public event Action? PreferenceChanged;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        var generation = ++_generation;
        _readCancellation?.Cancel();
        using var read = _operations.CreateOperation(cancellationToken);
        _readCancellation = read;
        _isLoading = true;
        _catalogAvailable = false;
        _hasLoadError = false;
        NotifyState();
        try
        {
            var profiles = await _repository.GetAllAsync(read.Token).WaitAsync(read.Token);
            if (_disposed || generation != _generation || read.IsCancellationRequested) return;
            var enabled = profiles.Where(static profile => profile.IsEnabled).ToArray();
            if (!Items.SequenceEqual(enabled)) Items.ReplaceWith(enabled);
            _catalogAvailable = true;
            _isLoading = false;
            if (PreferredProfileId is { } id && Find(id) is { } current) SetSelection(current);
            SynchronizeModel();
        }
        catch (Exception)
        {
            if (!_disposed && generation == _generation) _hasLoadError = true;
        }
        finally
        {
            if (ReferenceEquals(_readCancellation, read)) _readCancellation = null;
            if (!_disposed && generation == _generation)
            {
                _isLoading = false;
                NotifyState();
            }
        }
    }

    public void Select(AgentProfile? profile)
    {
        if (!CanSelect(profile)) return;
        _followAgentReasoning = true;
        SetSelection(profile is null ? null : Find(profile.Id));
        if (profile is null) _models.RestoreReasoning(null);
        SynchronizeModel();
        NotifyState();
        PreferenceChanged?.Invoke();
    }

    internal void RestoreConversation(Guid? profileId, bool followAgentReasoning)
    {
        _followAgentReasoning = followAgentReasoning;
        var profile = profileId is { } id ? Find(id) ?? (SelectedProfile?.Id == id ? SelectedProfile : null) : null;
        SetSelection(profile, profileId);
        NotifyState();
    }

    private bool CanSelect(AgentProfile? profile) => CanChoose && (profile is null || Find(profile.Id) is not null);
    private AgentProfile? Find(Guid id) => Items.FirstOrDefault(profile => profile.Id == id);
    private bool HasMatchingModel(AgentProfile profile) => _models.CanUseSelectedModel
        && _models.SelectedItem?.Provider.Id == profile.ProviderId
        && _models.SelectedItem.Model.Id == profile.ModelProfileId;

    private void SetSelection(AgentProfile? profile, Guid? preferredId = null)
    {
        SetProperty(ref _preferredProfileId, preferredId ?? profile?.Id, nameof(PreferredProfileId));
        if (ReferenceEquals(_selectedProfile, profile)) return;
        _selectedProfile = profile;
        OnPropertyChanged(nameof(SelectedProfile));
    }

    private void SynchronizeModel()
    {
        if (_synchronizingModel || !CanChoose || SelectedProfile is not { } profile
            || !ReferenceEquals(profile, Find(profile.Id))) return;
        _synchronizingModel = true;
        try
        {
            if (_models.TrySelectForAgent(profile.ProviderId, profile.ModelProfileId) && _followAgentReasoning)
                _models.RestoreReasoning(profile.ReasoningEffort);
        }
        finally { _synchronizingModel = false; }
    }

    private async Task RefreshAsync(CancellationToken token) =>
        await Task.WhenAll(_models.ReloadWithCancellationAsync(token), ReloadAsync(token));

    private void ClearForExplicitModelChoice()
    {
        if (_disposed) return;
        SetSelection(null);
        NotifyState();
    }

    private void HandleReasoningChoice() => _followAgentReasoning = false;

    private void HandleModelSelection(AiProvider? provider, ModelProfile? model)
    {
        if (_disposed || _synchronizingModel) return;
        SynchronizeModel();
        NotifyState();
    }

    private void HandleModelStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || _synchronizingModel || args.PropertyName != nameof(ModelPickerViewModel.CanUseSelectedModel)) return;
        SynchronizeModel();
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(HasAgents), nameof(IsLoading), nameof(HasLoadError), nameof(CanChoose),
            nameof(CanUseSelection), nameof(IsSelectedUnavailable), nameof(HasCatalogNotice), nameof(CatalogNotice), nameof(SelectedLabel) })
            OnPropertyChanged(property);
        SelectCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private static string GetLabel(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException($"Missing workspace resource: {key}");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _operations.Dispose();
        _models.ExplicitSelectionChanged -= ClearForExplicitModelChoice;
        _models.ModelSelected -= HandleModelSelection;
        _models.PropertyChanged -= HandleModelStateChanged;
        _models.PreferenceChanged -= HandleReasoningChoice;
        ((GuardedAsyncRelayCommand)RefreshCommand).Dispose();
        _isLoading = false;
        _catalogAvailable = false;
        NotifyState();
        PreferenceChanged = null;
    }
}
