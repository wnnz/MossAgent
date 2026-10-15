using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AgentSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private const int MaximumProfiles = 16;
    private readonly IAgentConfigurationRepository _agents;
    private readonly EnabledModelCatalog _modelCatalog;
    private readonly SettingsOperationLifetime _operations;
    private readonly SettingsMutationRunner _mutations = new();
    private readonly ModelReasoningOptions _reasoningOptions = new();
    private readonly AgentModelSelection _modelSelection = new();
    private AgentProfile? _selectedProfile;
    private AgentModelOption? _selectedModel;
    private Guid _newProfileId = Guid.NewGuid();
    private int _modelLoadVersion;
    private int _profileLoadVersion;
    private bool _initialized;
    private bool _modelsAvailable;
    private bool _profilesAvailable;
    private bool _publishingItems;
    private bool _publishingModels;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _disposed;
    private string _name = string.Empty;
    private string _systemPrompt = string.Empty;
    private string _reasoningEffort = "medium";
    private bool _isEnabled = true;
    private string _status = string.Empty;
    private bool _hasStatusError;
    private bool _isFormOpen;

    public AgentSettingsViewModel(IAgentConfigurationRepository agents,
        IConfigurationRepository configurations, TimeSpan? operationTimeout = null)
        : base(nameof(Name), nameof(SystemPrompt), nameof(ReasoningEffort), nameof(IsEnabled))
    {
        _agents = agents;
        _modelCatalog = new EnabledModelCatalog(configurations);
        _operations = new SettingsOperationLifetime(operationTimeout);
        _mutations.PropertyChanged += HandleMutationChanged;
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync,
            () => !_disposed && !IsLoading && !IsBusy);
        NewCommand = new RelayCommand(ResetEditor, () => !_disposed);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; }, () => !_disposed);
        OpenEditFormCommand = new RelayCommand<AgentProfile?>(OpenEditor, CanEdit);
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false, () => !_disposed);
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, CanMutate);
        DeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, () => CanMutate() && SelectedProfile is not null);
        _ = LoadAsync();
    }

    public event Func<Task>? ProfilesChanged;
    public ObservableCollection<AgentProfile> Items { get; } = [];
    public ObservableCollection<AgentModelOption> Models { get; } = [];
    public bool HasNoItems => Items.Count == 0;
    public bool HasModels => Models.Count > 0;
    public bool HasNoModels => Models.Count == 0;
    public bool IsBusy => _mutations.IsRunning;
    public bool IsLoading { get => _isLoading || _isRefreshing; private set => SetProperty(ref _isLoading, value); }
    public bool IsEditorTargetUnavailable => _profilesAvailable && !IsLoading && !IsBusy
        && SelectedProfile is { } profile && !Items.Any(item => item.Id == profile.Id);
    public bool HasReasoningError => SelectedModel is { } selected
        && !ModelReasoningPolicy.Supports(selected.Protocol, selected.Model.ModelId, ReasoningEffort);
    public string EditorNotice => IsEditorTargetUnavailable ? SettingsText.Get("AgentTargetUnavailable")
        : HasReasoningError ? ProviderSettingsText.Get("UnsupportedReasoningEffort") : string.Empty;
    public bool HasEditorNotice => IsEditorTargetUnavailable || HasReasoningError;
    public string SaveLabel => SettingsText.Get(IsBusy ? "AgentSaving" : "AgentSaveLabel");
    public IReadOnlyList<string> ReasoningEfforts => _reasoningOptions.ForEditor(
        SelectedModel?.Protocol, SelectedModel?.Model.ModelId ?? string.Empty, ReasoningEffort);
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<AgentProfile?> OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set { if (SetProperty(ref _isFormOpen, value)) InvalidateEditor(); }
    }

    public AgentProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_publishingItems || _disposed || !SetProperty(ref _selectedProfile, value)) return;
            InvalidateEditor();
            if (value is not null) LoadEditor(value);
            else _newProfileId = Guid.NewGuid();
            NotifyCommands();
        }
    }

    public AgentModelOption? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_publishingModels || _disposed) return;
            if (_modelSelection.Select(value)) InvalidateEditor();
            SetProperty(ref _selectedModel, value);
        }
    }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string SystemPrompt { get => _systemPrompt; set => SetProperty(ref _systemPrompt, value); }
    public string ReasoningEffort { get => _reasoningEffort; set { if (value is not null) SetProperty(ref _reasoningEffort, value); } }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public string EditorTitle => SettingsText.Get(SelectedProfile is null ? "AgentCreateTitle" : "AgentEditTitle");
    private Guid EditorId => SelectedProfile?.Id ?? _newProfileId;

    public Task<bool> LoadAsync(CancellationToken cancellationToken = default) =>
        !_disposed && !IsBusy ? ReadProfilesAsync(cancellationToken) : Task.FromResult(false);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var operation = _operations.CreateOperation(cancellationToken);
        var revision = EditorRevision;
        _isRefreshing = true;
        OnPropertyChanged(nameof(IsLoading));
        NotifyCommands();
        try
        {
            if (await LoadAsync(operation.Token)) await NotifyProfilesChangedAsync(operation.Token, revision);
            if (!_disposed && operation.IsCancellationRequested && revision == EditorRevision)
                SetStatus(cancellationToken.IsCancellationRequested ? "AgentOperationCancelled" : "AgentOperationTimedOut", true);
        }
        finally
        {
            if (!_disposed)
            {
                _isRefreshing = false;
                OnPropertyChanged(nameof(IsLoading));
                NotifyCommands();
            }
        }
    }

    private async Task<bool> ReadProfilesAsync(CancellationToken cancellationToken = default, bool reloadModels = true)
    {
        if (_disposed) return false;
        var version = Interlocked.Increment(ref _profileLoadVersion);
        var revision = EditorRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        IsLoading = true;
        _profilesAvailable = false;
        NotifyCommands();
        try
        {
            var profiles = await _agents.GetAllAsync(operation.Token).WaitAsync(operation.Token);
            if (reloadModels && !await ReloadModelsAsync(operation.Token)) return false;
            if (_disposed || operation.IsCancellationRequested || version != _profileLoadVersion) return false;
            _publishingItems = true;
            try { if (!Items.SequenceEqual(profiles)) Items.ReplaceWith(profiles); }
            finally { _publishingItems = false; }
            OnPropertyChanged(nameof(HasNoItems));
            if (!_initialized && revision == EditorRevision)
            {
                SelectedProfile = Items.FirstOrDefault();
            }
            else if (Items.FirstOrDefault(item => item.Id == SelectedProfile?.Id) is { } selected)
            {
                SetProfileReference(selected);
            }
            _initialized = true;
            _profilesAvailable = true;
            if (revision == EditorRevision && (Status == SettingsText.Get("AgentLoadFailed")
                || Status == SettingsText.Get("AgentOperationCancelled") || Status == SettingsText.Get("AgentOperationTimedOut")))
                ClearStatus();
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && revision == EditorRevision && version == _profileLoadVersion)
                SetStatus(cancellationToken.IsCancellationRequested ? "AgentOperationCancelled" : "AgentOperationTimedOut", true);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision && version == _profileLoadVersion) SetStatus("AgentLoadFailed", true);
            return false;
        }
        finally
        {
            if (!_disposed && version == _profileLoadVersion) { IsLoading = false; NotifyCommands(); }
        }
    }

    public async Task<bool> ReloadModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        var version = Interlocked.Increment(ref _modelLoadVersion);
        _modelsAvailable = false;
        NotifyCommands();
        using var operation = _operations.CreateOperation(cancellationToken);
        try
        {
            var ordered = await _modelCatalog.ReadAgentOptionsAsync(operation.Token);
            if (_disposed || operation.IsCancellationRequested || version != _modelLoadVersion) return false;
            _publishingModels = true;
            try
            {
                _selectedModel = _modelSelection.Publish(Models, ordered, SelectedProfile is null);
                OnPropertyChanged(nameof(SelectedModel));
            }
            finally { _publishingModels = false; }
            _modelsAvailable = true;
            NotifyCommands();
            OnPropertyChanged(nameof(HasModels));
            OnPropertyChanged(nameof(HasNoModels));
            if (_modelSelection.HasPreferredModel && SelectedModel is null) SetStatus("AgentModelUnavailable", true);
            else if (Status == SettingsText.Get("AgentModelUnavailable") || Status == SettingsText.Get("AgentModelLoadFailed"))
                ClearStatus();
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && version == _modelLoadVersion)
                SetStatus(cancellationToken.IsCancellationRequested ? "AgentOperationCancelled" : "AgentOperationTimedOut", true);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && version == _modelLoadVersion) SetStatus("AgentModelLoadFailed", true);
            return false;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate()) return;
        if (SelectedModel is null) { SetStatus("AgentInvalidModel", true); return; }
        var profile = new AgentProfile(EditorId, Name.Trim(), SystemPrompt.Trim(),
            SelectedModel.Model.ProviderId, SelectedModel.Model.Id, ReasoningEffort.Trim().ToLowerInvariant(), IsEnabled);
        var editing = SelectedProfile is not null;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await ReadProfilesAsync(token)) return;
            if (!TryValidate(profile, editing, out var error))
            {
                if (revision == EditorRevision) SetStatus(error!, true);
                return;
            }
            await _agents.SaveAsync(profile, token);
            if (_disposed || token.IsCancellationRequested) return;
            if (EditorId == profile.Id) SetProfileReference(profile);
            var notified = await NotifyProfilesChangedAsync(token, revision);
            if (!await ReadProfilesAsync(token, reloadModels: false) || !notified) return;
            if (revision == EditorRevision) { SetStatus("AgentSaved", false); IsFormOpen = false; }
            else if (EditorId == profile.Id) SetStatus("AgentPreviousDraftSaved", false);
        }, cancellationToken, revision, "AgentSaveFailed");
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate() || SelectedProfile is not { } profile) return;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await ReadProfilesAsync(token, reloadModels: false) || !Items.Any(item => item.Id == profile.Id)) return;
            await _agents.DeleteAsync(profile.Id, token);
            if (_disposed || token.IsCancellationRequested) return;
            var unchanged = revision == EditorRevision;
            if (unchanged) { ResetEditor(); IsFormOpen = false; }
            var notified = await NotifyProfilesChangedAsync(token, revision);
            if (await ReadProfilesAsync(token, reloadModels: false) && unchanged && notified) SetStatus("AgentDeleted", false);
        }, cancellationToken, revision, "AgentDeleteFailed");
    }

    private async Task<bool> NotifyProfilesChangedAsync(CancellationToken cancellationToken, long revision)
    {
        if (_disposed || cancellationToken.IsCancellationRequested) return false;
        try
        {
            if (ProfilesChanged is { } changed) await changed().WaitAsync(cancellationToken);
            if (!_disposed && revision == EditorRevision && Status == SettingsText.Get("AgentWorkspaceReloadFailed"))
                ClearStatus();
            return !_disposed && !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("AgentWorkspaceReloadFailed", true);
            return false;
        }
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken,
        long revision, string failureKey)
    {
        using var lifetime = _operations.CreateOperation(cancellationToken);
        await _mutations.TryExecuteAsync(operation, lifetime.Token,
            () => { if (!_disposed && revision == EditorRevision) SetStatus(failureKey, true); });
        if (!_disposed && lifetime.IsCancellationRequested && revision == EditorRevision)
            SetStatus(cancellationToken.IsCancellationRequested ? "AgentOperationCancelled" : "AgentOperationTimedOut", true);
    }

    private bool TryValidate(AgentProfile profile, bool editing, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 48
            || string.IsNullOrWhiteSpace(profile.SystemPrompt) || profile.SystemPrompt.Length > 16_000)
            error = "AgentRequiredFields";
        else if (Items.Any(item => item.Id != profile.Id && item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
            error = "AgentDuplicateName";
        else if (!Items.Any(item => item.Id == profile.Id) && Items.Count >= MaximumProfiles)
            error = "AgentMaximumProfiles";
        else if (editing && !Items.Any(item => item.Id == profile.Id))
            error = "AgentTargetUnavailable";
        else if (!Models.Any(option => option.Model.Id == profile.ModelProfileId && option.Model.ProviderId == profile.ProviderId
            && ModelReasoningPolicy.Supports(option.Protocol, option.Model.ModelId, profile.ReasoningEffort))) error = "AgentInvalidModel";
        return error is null;
    }

    private void LoadEditor(AgentProfile profile)
    {
        Name = profile.Name;
        SystemPrompt = profile.SystemPrompt;
        SetProperty(ref _selectedModel, _modelSelection.Restore(profile.ProviderId, profile.ModelProfileId, Models), nameof(SelectedModel));
        ReasoningEffort = profile.ReasoningEffort;
        IsEnabled = profile.IsEnabled;
        if (SelectedModel is null) SetStatus("AgentModelUnavailable", true);
        else ClearStatus();
    }

    private void ResetEditor()
    {
        if (_disposed) return;
        InvalidateEditor();
        SetProfileReference(null);
        _newProfileId = Guid.NewGuid();
        Name = string.Empty;
        SystemPrompt = string.Empty;
        SelectedModel = Models.FirstOrDefault();
        ReasoningEffort = SelectedModel?.Model.ReasoningEffort ?? "medium";
        IsEnabled = true;
        ClearStatus();
    }

    private bool CanEdit(AgentProfile? profile) => !_disposed && profile is not null
        && Items.Any(item => item.Id == profile.Id);

    private void OpenEditor(AgentProfile? target)
    {
        if (!CanEdit(target) || Items.FirstOrDefault(item => item.Id == target!.Id) is not { } profile) return;
        InvalidateEditor();
        SetProfileReference(profile);
        LoadEditor(profile);
        IsFormOpen = true;
    }

    private void SetProfileReference(AgentProfile? profile)
    {
        _publishingItems = true;
        try { _selectedProfile = profile; OnPropertyChanged(nameof(SelectedProfile)); }
        finally { _publishingItems = false; }
        NotifyCommands();
    }

    private bool CanMutate() => !_disposed && _profilesAvailable && _modelsAvailable && !IsLoading && !IsBusy && !IsEditorTargetUnavailable;
    private void SetStatus(string key, bool error) { HasStatusError = error; Status = SettingsText.Get(key); }
    private void ClearStatus() { HasStatusError = false; Status = string.Empty; }

    private void HandleMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(SaveLabel));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        OpenEditFormCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(IsEditorTargetUnavailable));
        OnPropertyChanged(nameof(EditorNotice));
        OnPropertyChanged(nameof(ReasoningEfforts));
        OnPropertyChanged(nameof(HasReasoningError));
        OnPropertyChanged(nameof(HasEditorNotice));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SelectedModel) or nameof(ReasoningEffort)) NotifyCommands();
        base.OnPropertyChanged(args);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsFormOpen = false;
        _operations.Dispose();
        _mutations.PropertyChanged -= HandleMutationChanged;
        ((IDisposable)RefreshCommand).Dispose();
        ((IDisposable)SaveCommand).Dispose();
        ((IDisposable)DeleteCommand).Dispose();
        NewCommand.NotifyCanExecuteChanged();
        OpenCreateFormCommand.NotifyCanExecuteChanged();
        OpenEditFormCommand.NotifyCanExecuteChanged();
        CloseFormCommand.NotifyCanExecuteChanged();
    }
}
