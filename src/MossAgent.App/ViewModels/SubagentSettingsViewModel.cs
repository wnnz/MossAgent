using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class SubagentSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private const int MaximumProfiles = 16;
    private readonly ISubagentConfigurationRepository _subagents;
    private readonly EnabledModelCatalog _modelCatalog;
    private readonly SettingsOperationLifetime _operations;
    private readonly SettingsMutationRunner _mutations = new();
    private readonly ModelReasoningOptions _reasoningOptions = new();
    private readonly AgentModelSelection _modelSelection = new();
    private SubagentProfile? _selectedProfile;
    private AgentModelOption? _selectedModel;
    private Guid _newProfileId = Guid.NewGuid();
    private int _modelLoadVersion;
    private int _profileLoadVersion;
    private bool _initialized;
    private bool _profilesAvailable;
    private bool _publishingItems;
    private bool _publishingModels;
    private bool _isLoading;
    private bool _disposed;
    private string _name = string.Empty;
    private string _triggerCondition = string.Empty;
    private string _systemPrompt = string.Empty;
    private string _reasoningEffort = "medium";
    private bool _shareContext = true;
    private bool _isEnabled = true;
    private string _status = string.Empty;
    private bool _hasStatusError;
    private bool _isFormOpen;

    public SubagentSettingsViewModel(ISubagentConfigurationRepository subagents,
        IConfigurationRepository configurations, TimeSpan? operationTimeout = null)
        : base(nameof(Name), nameof(TriggerCondition), nameof(SystemPrompt),
            nameof(ReasoningEffort), nameof(ShareContext), nameof(IsEnabled))
    {
        _subagents = subagents;
        _modelCatalog = new EnabledModelCatalog(configurations);
        _operations = new SettingsOperationLifetime(operationTimeout);
        _mutations.PropertyChanged += HandleMutationChanged;
        RefreshCommand = new GuardedAsyncRelayCommand(async token => { await LoadAsync(token); },
            () => !_disposed && !IsLoading && !IsBusy);
        NewCommand = new RelayCommand(ResetEditor, () => !_disposed);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; }, () => !_disposed);
        OpenEditFormCommand = new RelayCommand<SubagentProfile?>(OpenEditor, CanEdit);
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false, () => !_disposed);
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, CanMutate);
        DeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, () => CanMutate() && SelectedProfile is not null);
        _ = LoadAsync();
    }

    public ObservableCollection<SubagentProfile> Items { get; } = [];
    public ObservableCollection<AgentModelOption> Models { get; } = [];
    public bool HasNoItems => Items.Count == 0;
    public bool HasModels => Models.Count > 0;
    public bool HasNoModels => Models.Count == 0;
    public bool IsBusy => _mutations.IsRunning;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsEditorTargetUnavailable => SelectedProfile is { } profile && !Items.Any(item => item.Id == profile.Id);
    public bool HasReasoningError => SelectedModel is { } selected
        && !ModelReasoningPolicy.Supports(selected.Protocol, selected.Model.ModelId, ReasoningEffort);
    public string EditorNotice => IsEditorTargetUnavailable ? SettingsText.Get("SubagentTargetUnavailable")
        : HasReasoningError ? ProviderSettingsText.Get("UnsupportedReasoningEffort") : string.Empty;
    public bool HasEditorNotice => IsEditorTargetUnavailable || HasReasoningError;
    public string SaveLabel => SettingsText.Get(IsBusy ? "SubagentSaving" : "SubagentSaveLabel");
    public IReadOnlyList<string> ReasoningEfforts => _reasoningOptions.ForEditor(
        SelectedModel?.Protocol, SelectedModel?.Model.ModelId ?? string.Empty, ReasoningEffort);
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<SubagentProfile?> OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set { if (SetProperty(ref _isFormOpen, value)) InvalidateEditor(); }
    }

    public SubagentProfile? SelectedProfile
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
    public string TriggerCondition { get => _triggerCondition; set => SetProperty(ref _triggerCondition, value); }
    public string SystemPrompt { get => _systemPrompt; set => SetProperty(ref _systemPrompt, value); }
    public string ReasoningEffort { get => _reasoningEffort; set { if (value is not null) SetProperty(ref _reasoningEffort, value); } }
    public bool ShareContext { get => _shareContext; set => SetProperty(ref _shareContext, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public string EditorTitle => SettingsText.Get(SelectedProfile is null ? "SubagentCreateTitle" : "SubagentEditTitle");
    private Guid EditorId => SelectedProfile?.Id ?? _newProfileId;

    private async Task<bool> LoadAsync(CancellationToken cancellationToken = default, bool reloadModels = true)
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
            var profiles = await _subagents.GetAllAsync(operation.Token).WaitAsync(operation.Token);
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
            if (revision == EditorRevision && (Status == SettingsText.Get("SubagentLoadFailed")
                || Status == SettingsText.Get("SubagentOperationCancelled") || Status == SettingsText.Get("SubagentOperationTimedOut")))
                ClearStatus();
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && revision == EditorRevision && version == _profileLoadVersion)
                SetStatus(cancellationToken.IsCancellationRequested ? "SubagentOperationCancelled" : "SubagentOperationTimedOut", true);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision && version == _profileLoadVersion) SetStatus("SubagentLoadFailed", true);
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
            OnPropertyChanged(nameof(HasModels));
            OnPropertyChanged(nameof(HasNoModels));
            if (_modelSelection.HasPreferredModel && SelectedModel is null) SetStatus("SubagentModelUnavailable", true);
            else if (Status == SettingsText.Get("SubagentModelUnavailable") || Status == SettingsText.Get("SubagentModelLoadFailed"))
                ClearStatus();
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && version == _modelLoadVersion)
                SetStatus(cancellationToken.IsCancellationRequested ? "SubagentOperationCancelled" : "SubagentOperationTimedOut", true);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && version == _modelLoadVersion) SetStatus("SubagentModelLoadFailed", true);
            return false;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate()) return;
        if (SelectedModel is null) { SetStatus("SubagentInvalidModel", true); return; }
        var profile = new SubagentProfile(EditorId, Name.Trim(), TriggerCondition.Trim(), SystemPrompt.Trim(),
            SelectedModel.Model.ProviderId, SelectedModel.Model.Id, ReasoningEffort.Trim().ToLowerInvariant(), ShareContext, IsEnabled);
        var editing = SelectedProfile is not null;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await LoadAsync(token)) return;
            if (!TryValidate(profile, editing, out var error))
            {
                if (revision == EditorRevision) SetStatus(error!, true);
                return;
            }
            await _subagents.SaveAsync(profile, token);
            if (_disposed || token.IsCancellationRequested) return;
            if (EditorId == profile.Id) SetProfileReference(profile);
            if (!await LoadAsync(token, reloadModels: false)) return;
            if (revision == EditorRevision) { SetStatus("SubagentSaved", false); IsFormOpen = false; }
            else if (EditorId == profile.Id) SetStatus("SubagentPreviousDraftSaved", false);
        }, cancellationToken, revision, "SubagentSaveFailed");
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate() || SelectedProfile is not { } profile) return;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await LoadAsync(token, reloadModels: false) || !Items.Any(item => item.Id == profile.Id)) return;
            await _subagents.DeleteAsync(profile.Id, token);
            if (_disposed || token.IsCancellationRequested) return;
            var unchanged = revision == EditorRevision;
            if (unchanged) { ResetEditor(); IsFormOpen = false; }
            if (await LoadAsync(token, reloadModels: false) && unchanged) SetStatus("SubagentDeleted", false);
        }, cancellationToken, revision, "SubagentDeleteFailed");
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken,
        long revision, string failureKey)
    {
        using var lifetime = _operations.CreateOperation(cancellationToken);
        await _mutations.TryExecuteAsync(operation, lifetime.Token,
            () => { if (!_disposed && revision == EditorRevision) SetStatus(failureKey, true); });
        if (!_disposed && lifetime.IsCancellationRequested && revision == EditorRevision)
            SetStatus(cancellationToken.IsCancellationRequested ? "SubagentOperationCancelled" : "SubagentOperationTimedOut", true);
    }

    private bool TryValidate(SubagentProfile profile, bool editing, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 48
            || string.IsNullOrWhiteSpace(profile.TriggerCondition) || profile.TriggerCondition.Length > 500
            || string.IsNullOrWhiteSpace(profile.SystemPrompt) || profile.SystemPrompt.Length > 16_000)
            error = "SubagentRequiredFields";
        else if (Items.Any(item => item.Id != profile.Id && item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
            error = "SubagentDuplicateName";
        else if (!Items.Any(item => item.Id == profile.Id) && Items.Count >= MaximumProfiles)
            error = "SubagentMaximumProfiles";
        else if (editing && !Items.Any(item => item.Id == profile.Id))
            error = "SubagentTargetUnavailable";
        else if (!Models.Any(option => option.Model.Id == profile.ModelProfileId && option.Model.ProviderId == profile.ProviderId
            && ModelReasoningPolicy.Supports(option.Protocol, option.Model.ModelId, profile.ReasoningEffort))) error = "SubagentInvalidModel";
        return error is null;
    }

    private void LoadEditor(SubagentProfile profile)
    {
        Name = profile.Name;
        TriggerCondition = profile.TriggerCondition;
        SystemPrompt = profile.SystemPrompt;
        SetProperty(ref _selectedModel, _modelSelection.Restore(profile.ProviderId, profile.ModelProfileId, Models), nameof(SelectedModel));
        ReasoningEffort = profile.ReasoningEffort;
        ShareContext = profile.ShareContext;
        IsEnabled = profile.IsEnabled;
        if (SelectedModel is null) SetStatus("SubagentModelUnavailable", true);
        else ClearStatus();
    }

    private bool CanEdit(SubagentProfile? profile) => !_disposed && profile is not null
        && Items.Any(item => item.Id == profile.Id);

    private void OpenEditor(SubagentProfile? target)
    {
        if (!CanEdit(target) || Items.FirstOrDefault(item => item.Id == target!.Id) is not { } profile) return;
        InvalidateEditor();
        SetProfileReference(profile);
        LoadEditor(profile);
        IsFormOpen = true;
    }

    private void ResetEditor()
    {
        if (_disposed) return;
        InvalidateEditor();
        SetProfileReference(null);
        _newProfileId = Guid.NewGuid();
        Name = string.Empty;
        TriggerCondition = string.Empty;
        SystemPrompt = string.Empty;
        SelectedModel = Models.FirstOrDefault();
        ReasoningEffort = SelectedModel?.Model.ReasoningEffort ?? "medium";
        ShareContext = true;
        IsEnabled = true;
        ClearStatus();
    }

    private void SetProfileReference(SubagentProfile? profile)
    {
        _publishingItems = true;
        try { _selectedProfile = profile; OnPropertyChanged(nameof(SelectedProfile)); }
        finally { _publishingItems = false; }
        NotifyCommands();
    }

    private bool CanMutate() => !_disposed && _profilesAvailable && !IsLoading && !IsBusy && !IsEditorTargetUnavailable;
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
