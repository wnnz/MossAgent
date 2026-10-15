using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ModelSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private const int DefaultContextLength = 128000;
    private const int DefaultMaximumOutputTokens = 8192;
    private readonly IConfigurationRepository _repository;
    private readonly ProviderSettingsViewModel _providerSettings;
    private readonly CancellationTokenSource _lifetime;
    private readonly ModelDiscoveryOperation _discoveryOperation;
    private readonly ModelReasoningOptions _reasoningOptions = new();
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private Guid _newModelId = Guid.NewGuid();
    private string _modelId = string.Empty;
    private string _displayName = string.Empty;
    private int _contextLength = DefaultContextLength;
    private int _maximumOutputTokens = DefaultMaximumOutputTokens;
    private string _reasoningEffort = "medium";
    private double? _temperature;
    private double? _topP;
    private bool _supportsImages;
    private bool _supportsFiles;
    private bool _isDefault;
    private bool _isEnabled = true;
    private string _advancedJson = "{}";
    private string _status = string.Empty;
    private bool _hasStatusError;
    private string _editorStatus = string.Empty;
    private bool _isFormOpen;
    private bool _disposed;

    public ModelSettingsViewModel(
        IConfigurationRepository repository,
        IModelDiscoveryService discovery,
        ProviderSettingsViewModel providerSettings,
        TimeSpan? discoveryTimeout = null)
        : base(nameof(SelectedProvider), nameof(SelectedModel), nameof(IsFormOpen), nameof(ModelId), nameof(DisplayName),
            nameof(ContextLength), nameof(MaximumOutputTokens), nameof(ReasoningEffort), nameof(Temperature), nameof(TopP),
            nameof(SupportsImages), nameof(SupportsFiles), nameof(IsDefault), nameof(IsEnabled), nameof(AdvancedJson))
    {
        _repository = repository;
        _providerSettings = providerSettings;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(Catalog.LifetimeToken);
        _discoveryOperation = new ModelDiscoveryOperation(repository, discovery, Catalog, discoveryTimeout);
        _providerSettings.ProvidersChanged += NotifyModelCatalogChangedAsync;
        Catalog.SnapshotChanged += SynchronizeCatalog;
        Catalog.PropertyChanged += HandleCatalogStateChanged;
        RefreshProvidersCommand = new AsyncRelayCommand(LoadProvidersAsync, () => !_disposed && !Catalog.IsLoading && !Catalog.Mutations.IsRunning);
        FetchModelsCommand = new AsyncRelayCommand(FetchModelsAsync, HasProvider);
        FetchModelsForProviderCommand = new AsyncRelayCommand<ProviderModelGroupViewModel>(FetchModelsForProviderAsync, CanUseGroup);
        SaveModelCommand = new AsyncRelayCommand(SaveModelAsync, HasProvider);
        DeleteModelCommand = new AsyncRelayCommand(DeleteModelAsync, HasModel);
        DeleteProviderModelCommand = new AsyncRelayCommand<ModelProfile>(DeleteProviderModelAsync, CanUseModel);
        NewModelCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        AddModelForProviderCommand = new RelayCommand<ProviderModelGroupViewModel>(OpenCreateModelForProvider, CanUseGroup);
        EditProviderModelCommand = new RelayCommand<ModelProfile>(OpenEditModelForProvider, CanUseModel);
        ToggleProviderCommand = new RelayCommand<ProviderModelGroupViewModel>(ToggleProvider);
        OpenEditFormCommand = new RelayCommand(() => OpenEditModelForProvider(SelectedModel));
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        SynchronizeCatalog();
    }

    public ProviderModelCatalogViewModel Catalog => _providerSettings.Catalog;
    public ObservableCollection<AiProvider> Providers => Catalog.Providers;
    public ObservableCollection<ModelProfile> Models { get; } = [];
    public ObservableCollection<ProviderModelGroupViewModel> ProviderGroups => Catalog.Groups;
    public bool HasProviderGroups => ProviderGroups.Count > 0;
    public event Func<Task>? ModelCatalogChanged;
    public IReadOnlyList<string> ReasoningEfforts => _reasoningOptions.ForEditor(SelectedProvider?.Protocol, ModelId, ReasoningEffort);
    public IAsyncRelayCommand RefreshProvidersCommand { get; }
    public IAsyncRelayCommand FetchModelsCommand { get; }
    public IAsyncRelayCommand<ProviderModelGroupViewModel> FetchModelsForProviderCommand { get; }
    public IAsyncRelayCommand SaveModelCommand { get; }
    public IAsyncRelayCommand DeleteModelCommand { get; }
    public IAsyncRelayCommand<ModelProfile> DeleteProviderModelCommand { get; }
    public IRelayCommand NewModelCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<ProviderModelGroupViewModel> AddModelForProviderCommand { get; }
    public IRelayCommand<ModelProfile> EditProviderModelCommand { get; }
    public IRelayCommand<ProviderModelGroupViewModel> ToggleProviderCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set
        {
            if (!SetProperty(ref _isFormOpen, value)) return;
            EditorStatus = string.Empty;
            NotifyCommandState();
        }
    }

    public AiProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            var previousProviderId = _selectedProvider?.Id;
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            NotifyCommandState();
            if (previousProviderId != value?.Id) ResetEditor();
            ApplySelectedModels();
        }
    }

    public ModelProfile? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value) && value is not null)
            {
                LoadEditor(value);
            }

            NotifyCommandState();
            OnPropertyChanged(nameof(IsEditing));
        }
    }

    public bool IsEditing => SelectedModel is not null;
    public bool IsEditorTargetUnavailable => SelectedProvider is not { } provider
        || Catalog.FindProvider(provider.Id) is null
        || SelectedModel is { } model && (model.ProviderId != provider.Id || FindModel(model) is null);
    public bool HasReasoningError => !ModelReasoningPolicy.Supports(SelectedProvider?.Protocol, ModelId, ReasoningEffort);
    public string EditorNotice => IsEditorTargetUnavailable ? ProviderSettingsText.Get("ModelTargetUnavailable")
        : Catalog.HasNotice ? Catalog.Notice : HasReasoningError ? ProviderSettingsText.Get("UnsupportedReasoningEffort") : string.Empty;
    public bool HasEditorNotice => IsEditorTargetUnavailable || Catalog.HasNotice || HasReasoningError;
    public string ModelId { get => _modelId; set => SetProperty(ref _modelId, value); }
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
    public int ContextLength { get => _contextLength; set => SetProperty(ref _contextLength, value); }
    public int MaximumOutputTokens { get => _maximumOutputTokens; set => SetProperty(ref _maximumOutputTokens, value); }
    public string ReasoningEffort { get => _reasoningEffort; set { if (value is not null) SetProperty(ref _reasoningEffort, value); } }
    public double? Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }
    public double? TopP { get => _topP; set => SetProperty(ref _topP, value); }
    public bool SupportsImages { get => _supportsImages; set => SetProperty(ref _supportsImages, value); }
    public bool SupportsFiles { get => _supportsFiles; set => SetProperty(ref _supportsFiles, value); }
    public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string AdvancedJson { get => _advancedJson; set => SetProperty(ref _advancedJson, value); }
    public string Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string EditorStatus
    {
        get => _editorStatus;
        private set
        {
            if (SetProperty(ref _editorStatus, value)) OnPropertyChanged(nameof(HasEditorStatus));
        }
    }
    public bool HasEditorStatus => !string.IsNullOrWhiteSpace(EditorStatus);
    public bool HasStatusError
    {
        get => _hasStatusError;
        private set => SetProperty(ref _hasStatusError, value);
    }

    private async Task LoadProvidersAsync()
    {
        if (_disposed || Catalog.Mutations.IsRunning) return;
        await RefreshModelCatalogAsync();
    }

    private void ApplySelectedModels()
    {
        var group = Catalog.FindGroup(SelectedProvider?.Id);
        Models.ReplaceWith(group?.Models ?? Enumerable.Empty<ModelProfile>());
    }

    private Task FetchModelsAsync() =>
        SelectedProvider is { } provider
            ? FetchModelsAsync(provider)
            : Task.CompletedTask;

    private Task FetchModelsAsync(AiProvider provider) => _disposed ? Task.CompletedTask
        : _discoveryOperation.ExecuteAsync(provider, CreateDiscoveredModel, _lifetime.Token, RefreshModelCatalogAsync, SetStatus);

    private static ModelProfile CreateDiscoveredModel(AiProvider provider, DiscoveredModel item)
    {
        var capabilities = ModelCapabilityCatalog.Find(provider.Protocol, item.Id);
        var context = capabilities?.ContextLength ?? DefaultContextLength;
        var output = Math.Min(capabilities?.MaximumOutputTokens ?? DefaultMaximumOutputTokens, Math.Min(1_000_000, context - 256));
        return new(Guid.NewGuid(), provider.Id, item.Id, item.DisplayName,
            context, output, ModelReasoningPolicy.DefaultEffort(provider.Protocol, item.Id),
            null, null, capabilities?.SupportsImages ?? false, capabilities?.SupportsFiles ?? false, false, true, "{}");
    }

    private async Task SaveModelAsync()
    {
        if (!HasProvider()) return;
        var error = ModelSettingsValidator.Validate(SelectedProvider?.Protocol, ModelId,
            ContextLength, MaximumOutputTokens, ReasoningEffort, Temperature, TopP, AdvancedJson);
        if (error is not null)
        {
            SetEditorError(error);
            return;
        }

        var modelId = ModelId.Trim();
        var displayName = DisplayName.Trim();
        var model = new ModelProfile(
            SelectedModel?.Id ?? _newModelId, SelectedProvider!.Id, modelId,
            string.IsNullOrWhiteSpace(displayName) ? modelId : displayName,
            ContextLength, MaximumOutputTokens, ReasoningEffort.Trim().ToLowerInvariant(),
            Temperature, TopP, SupportsImages, SupportsFiles, IsDefault, IsEnabled, AdvancedJson);
        var editing = IsEditing;
        var revision = EditorRevision;
        if (!await Catalog.Mutations.TryExecuteAsync(token => _repository.SaveModelAsync(model, token),
            _lifetime.Token, message => { if (!_disposed && revision == EditorRevision) SetEditorError(message); }, "ModelSaveFailed"))
        {
            await RefreshModelCatalogAsync();
            return;
        }
        var unchanged = revision == EditorRevision;
        if (SelectedModel is null && _newModelId == model.Id)
        {
            SetProperty(ref _selectedModel, model, nameof(SelectedModel));
            OnPropertyChanged(nameof(IsEditing));
        }
        if (unchanged)
        {
            IsFormOpen = false;
            SetStatus(ProviderSettingsText.Get(editing ? "ModelUpdated" : "ModelAdded"), false);
            ResetEditor();
        }
        await RefreshModelCatalogAsync();
    }

    private Task DeleteModelAsync() => DeleteProviderModelAsync(SelectedModel);

    private async Task DeleteProviderModelAsync(ModelProfile? model)
    {
        model = FindEditableModel(model);
        if (model is null)
        {
            return;
        }

        var revision = EditorRevision;
        if (!await Catalog.Mutations.TryExecuteAsync(token => _repository.DeleteModelAsync(model.Id, token),
            _lifetime.Token, message => { if (!_disposed && revision == EditorRevision) SetStatusError(message); }, "ModelDeleteFailed")) return;
        SetStatus(ProviderSettingsText.Format("ModelDeleted", model.DisplayName), false);
        if (revision == EditorRevision && SelectedModel?.Id == model.Id)
        {
            IsFormOpen = false;
            ResetEditor();
        }
        await RefreshModelCatalogAsync();
    }

    private Task FetchModelsForProviderAsync(ProviderModelGroupViewModel? group)
    {
        var provider = FindEditableProvider(group?.Provider.Id);
        if (provider is null)
        {
            return Task.CompletedTask;
        }

        SelectedProvider = provider;
        return FetchModelsAsync(provider);
    }

    private async Task RefreshModelCatalogAsync()
    {
        if (!_disposed) await Catalog.RefreshAsync();
        await NotifyModelCatalogChangedAsync();
    }

    private void ToggleProvider(ProviderModelGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
        if (group.IsExpanded)
        {
            SelectedProvider = group.Provider;
        }
    }

    private void OpenCreateModelForProvider(ProviderModelGroupViewModel? group)
    {
        var provider = FindEditableProvider(group?.Provider.Id);
        if (provider is null)
        {
            return;
        }

        SelectedProvider = provider;
        ResetEditor();
        IsFormOpen = true;
    }

    private void OpenEditModelForProvider(ModelProfile? model)
    {
        model = FindEditableModel(model);
        if (model is null)
        {
            return;
        }

        var reload = !IsFormOpen && Equals(SelectedModel, model);
        SelectedProvider = Catalog.FindProvider(model.ProviderId);
        SelectedModel = model;
        if (reload) LoadEditor(model);
        IsFormOpen = true;
    }

    private void LoadEditor(ModelProfile model)
    {
        EditorStatus = string.Empty;
        ModelId = model.ModelId;
        DisplayName = model.DisplayName;
        ContextLength = model.ContextLength;
        MaximumOutputTokens = model.MaximumOutputTokens;
        ReasoningEffort = model.ReasoningEffort;
        Temperature = model.Temperature;
        TopP = model.TopP;
        SupportsImages = model.SupportsImages;
        SupportsFiles = model.SupportsFiles;
        IsDefault = model.IsDefault;
        IsEnabled = model.IsEnabled;
        AdvancedJson = model.AdvancedParametersJson;
    }

    private void ResetEditor()
    {
        InvalidateEditor();
        EditorStatus = string.Empty;
        _newModelId = Guid.NewGuid();
        SelectedModel = null;
        ModelId = string.Empty;
        DisplayName = string.Empty;
        ContextLength = DefaultContextLength;
        MaximumOutputTokens = DefaultMaximumOutputTokens;
        ReasoningEffort = "medium";
        Temperature = null;
        TopP = null;
        SupportsImages = false;
        SupportsFiles = false;
        IsDefault = false;
        IsEnabled = true;
        AdvancedJson = "{}";
    }

    private bool HasProvider() => !_disposed && Catalog.CanMutate && !IsEditorTargetUnavailable;
    private bool HasModel() => HasProvider() && SelectedModel is not null;
    private bool CanUseGroup(ProviderModelGroupViewModel? group) => FindEditableProvider(group?.Provider.Id) is not null;
    private AiProvider? FindEditableProvider(Guid? id) => !_disposed && Catalog.CanMutate ? Catalog.FindProvider(id) : null;
    private bool CanUseModel(ModelProfile? model) => FindEditableModel(model) is not null;
    private ModelProfile? FindEditableModel(ModelProfile? model) =>
        !_disposed && Catalog.CanMutate ? FindModel(model) : null;
    private ModelProfile? FindModel(ModelProfile? model) => model is null ? null
        : Catalog.FindModel(model.ProviderId, model.Id);

    private async Task NotifyModelCatalogChangedAsync()
    {
        if (!_disposed && ModelCatalogChanged is { } modelCatalogChanged)
        {
            await modelCatalogChanged();
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (_disposed) return;
        HasStatusError = isError;
        Status = message;
    }

    private void SetStatusError(string message) => SetStatus(message, true);

    private void SetEditorError(string message)
    {
        SetStatusError(message);
        EditorStatus = message;
    }

    private void NotifyCommandState()
    {
        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveModelCommand.NotifyCanExecuteChanged();
        DeleteModelCommand.NotifyCanExecuteChanged();
        FetchModelsForProviderCommand.NotifyCanExecuteChanged();
        AddModelForProviderCommand.NotifyCanExecuteChanged();
        EditProviderModelCommand.NotifyCanExecuteChanged();
        DeleteProviderModelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsEditorTargetUnavailable));
        OnPropertyChanged(nameof(EditorNotice));
        OnPropertyChanged(nameof(HasEditorNotice));
        OnPropertyChanged(nameof(HasReasoningError));
        OnPropertyChanged(nameof(ReasoningEfforts));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ModelId) or nameof(ReasoningEffort) or nameof(SelectedProvider)) NotifyCommandState();
        base.OnPropertyChanged(args);
    }

    private void SynchronizeCatalog()
    {
        var provider = Catalog.FindProvider(SelectedProvider?.Id);
        if (IsFormOpen)
        {
            if (provider is not null) SetProperty(ref _selectedProvider, provider, nameof(SelectedProvider));
            var target = SelectedModel ?? ProviderGroups.SelectMany(group => group.Models)
                .FirstOrDefault(model => model.Id == _newModelId && model.ProviderId == SelectedProvider?.Id);
            if (FindModel(target) is { } model) SetProperty(ref _selectedModel, model, nameof(SelectedModel));
            OnPropertyChanged(nameof(IsEditing));
        }
        else
        {
            SelectedProvider = provider ?? Providers.FirstOrDefault();
            SelectedModel = FindModel(SelectedModel);
        }
        ApplySelectedModels();
        OnPropertyChanged(nameof(HasProviderGroups));
        NotifyCommandState();
    }

    private void HandleCatalogStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshProvidersCommand.NotifyCanExecuteChanged();
        NotifyCommandState();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _providerSettings.ProvidersChanged -= NotifyModelCatalogChangedAsync;
        Catalog.SnapshotChanged -= SynchronizeCatalog;
        Catalog.PropertyChanged -= HandleCatalogStateChanged;
        ModelCatalogChanged = null;
        HandleCatalogStateChanged(this, new PropertyChangedEventArgs(null));
    }
}
