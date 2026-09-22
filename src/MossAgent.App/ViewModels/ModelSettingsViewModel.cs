using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ModelSettingsViewModel : ObservableObject
{
    private readonly IConfigurationRepository _repository;
    private readonly IModelDiscoveryService _discovery;
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private string _modelId = string.Empty;
    private string _displayName = string.Empty;
    private int _contextLength = 128000;
    private int _maximumOutputTokens = 8192;
    private string _reasoningEffort = "medium";
    private double? _temperature;
    private double? _topP;
    private bool _supportsImages;
    private bool _supportsFiles;
    private bool _isDefault;
    private bool _isEnabled = true;
    private string _advancedJson = "{}";
    private string _status = string.Empty;
    private bool _isFormOpen;

    public ModelSettingsViewModel(
        IConfigurationRepository repository,
        IModelDiscoveryService discovery)
    {
        _repository = repository;
        _discovery = discovery;
        RefreshProvidersCommand = new AsyncRelayCommand(LoadProvidersAsync);
        FetchModelsCommand = new AsyncRelayCommand(FetchModelsAsync, HasProvider);
        SaveModelCommand = new AsyncRelayCommand(SaveModelAsync, HasProvider);
        DeleteModelCommand = new AsyncRelayCommand(DeleteModelAsync, HasModel);
        NewModelCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand(() => { if (SelectedModel is not null) IsFormOpen = true; });
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        _ = LoadProvidersAsync();
    }

    public ObservableCollection<AiProvider> Providers { get; } = [];
    public ObservableCollection<ModelProfile> Models { get; } = [];
    public IReadOnlyList<string> ReasoningEfforts { get; } =
        ["none", "minimal", "low", "medium", "high", "xhigh"];
    public IAsyncRelayCommand RefreshProvidersCommand { get; }
    public IAsyncRelayCommand FetchModelsCommand { get; }
    public IAsyncRelayCommand SaveModelCommand { get; }
    public IAsyncRelayCommand DeleteModelCommand { get; }
    public IRelayCommand NewModelCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }

    /// <summary>
    /// 是否弹出模型配置表单弹窗。
    /// </summary>
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set => SetProperty(ref _isFormOpen, value);
    }

    public AiProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            NotifyCommandState();
            ResetEditor();
            _ = LoadModelsAsync();
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

            DeleteModelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsEditing));
        }
    }

    public bool IsEditing => SelectedModel is not null;
    public string ModelId { get => _modelId; set => SetProperty(ref _modelId, value); }
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
    public int ContextLength { get => _contextLength; set => SetProperty(ref _contextLength, value); }
    public int MaximumOutputTokens { get => _maximumOutputTokens; set => SetProperty(ref _maximumOutputTokens, value); }
    public string ReasoningEffort { get => _reasoningEffort; set => SetProperty(ref _reasoningEffort, value); }
    public double? Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }
    public double? TopP { get => _topP; set => SetProperty(ref _topP, value); }
    public bool SupportsImages { get => _supportsImages; set => SetProperty(ref _supportsImages, value); }
    public bool SupportsFiles { get => _supportsFiles; set => SetProperty(ref _supportsFiles, value); }
    public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string AdvancedJson { get => _advancedJson; set => SetProperty(ref _advancedJson, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private async Task LoadProvidersAsync()
    {
        var selectedId = SelectedProvider?.Id;
        var providers = await _repository.GetProvidersAsync(CancellationToken.None);
        Providers.ReplaceWith(providers);
        SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == selectedId)
            ?? Providers.FirstOrDefault();
    }

    private async Task LoadModelsAsync()
    {
        Models.Clear();
        if (SelectedProvider is null)
        {
            return;
        }

        Models.ReplaceWith(await _repository.GetModelsAsync(
            SelectedProvider.Id, CancellationToken.None));
    }

    private async Task FetchModelsAsync()
    {
        try
        {
            var discovered = await _discovery.DiscoverAsync(
                SelectedProvider!, CancellationToken.None);
            foreach (var item in discovered.Where(item => Models.All(model => model.ModelId != item.Id)))
            {
                await _repository.SaveModelAsync(
                    CreateModel(Guid.NewGuid(), item.Id, item.DisplayName, isDefault: false),
                    CancellationToken.None);
            }

            await LoadModelsAsync();
            Status = $"已同步 {discovered.Count} 个模型。";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task SaveModelAsync()
    {
        var error = ModelSettingsValidator.Validate(
            SelectedProvider is not null, ModelId, Temperature, TopP, AdvancedJson);
        if (error is not null)
        {
            Status = error;
            return;
        }

        var id = SelectedModel?.Id ?? Guid.NewGuid();
        await _repository.SaveModelAsync(
            CreateModel(id, ModelId.Trim(), DisplayName.Trim(), IsDefault),
            CancellationToken.None);
        IsFormOpen = false;
        Status = SelectedModel is null ? "模型已添加。" : "模型设置已更新。";
        ResetEditor();
        await LoadModelsAsync();
    }

    private async Task DeleteModelAsync()
    {
        var model = SelectedModel;
        if (model is null)
        {
            return;
        }

        await _repository.DeleteModelAsync(model.Id, CancellationToken.None);
        Status = $"已删除模型：{model.DisplayName}";
        ResetEditor();
        await LoadModelsAsync();
    }

    private ModelProfile CreateModel(
        Guid id,
        string modelId,
        string displayName,
        bool isDefault) =>
        new(
            id, SelectedProvider!.Id, modelId,
            string.IsNullOrWhiteSpace(displayName) ? modelId : displayName,
            ContextLength, MaximumOutputTokens, ReasoningEffort,
            Temperature, TopP, SupportsImages, SupportsFiles,
            isDefault, IsEnabled, AdvancedJson);

    private void LoadEditor(ModelProfile model)
    {
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
        SelectedModel = null;
        ModelId = string.Empty;
        DisplayName = string.Empty;
        Temperature = null;
        TopP = null;
        SupportsImages = false;
        SupportsFiles = false;
        IsDefault = false;
        IsEnabled = true;
        AdvancedJson = "{}";
    }

    private bool HasProvider() => SelectedProvider is not null;
    private bool HasModel() => SelectedModel is not null;

    private void NotifyCommandState()
    {
        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveModelCommand.NotifyCanExecuteChanged();
        DeleteModelCommand.NotifyCanExecuteChanged();
    }
}
