using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

/// <summary>
/// 模型选择器弹窗视图模型，提供按供应商分组的模型检索、选择及思考强度快速切换。
/// </summary>
public sealed class ModelPickerViewModel : ObservableObject, IDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.ModelPickerStrings", typeof(ModelPickerViewModel).Assembly);
    private readonly EnabledModelCatalog _modelCatalog;
    private readonly SettingsOperationLifetime _operations;
    private CancellationTokenSource? _readCancellation;
    private readonly List<ModelPickerGroupViewModel> _allGroups = [];
    private string _searchQuery = string.Empty;
    private ModelPickerItemViewModel? _selectedItem;
    private IReadOnlyList<string> _reasoningSteps = ReasoningStepsFor(null);
    private string _selectedReasoning = UiText.T("默认");
    private bool _isModelListVisible;
    private bool _isPickerOpen;
    private Guid? _preferredProviderId;
    private Guid? _preferredModelId;
    private int _loadVersion;
    private bool _isLoading;
    private bool _catalogAvailable;
    private bool _hasLoadError;
    private bool _disposed;
    private bool _conversationBlocked;
    private bool _restoringReasoning;
    private string? _conversationNotice;

    public ModelPickerViewModel(IConfigurationRepository configurations)
        : this(configurations, TimeSpan.FromSeconds(30)) { }

    internal ModelPickerViewModel(IConfigurationRepository configurations, TimeSpan readTimeout)
    {
        _modelCatalog = new EnabledModelCatalog(configurations);
        _operations = new SettingsOperationLifetime(readTimeout);
        FilteredGroups = [];
        SelectItemCommand = new RelayCommand<ModelPickerItemViewModel>(SelectItem, CanSelectItem);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshCatalogAsync, () => !_disposed && !IsLoading);
        ShowModelListCommand = new RelayCommand(() => IsModelListVisible = true);
        ShowQuickMenuCommand = new RelayCommand(() => IsModelListVisible = !CanUseSelectedModel);
        ResetReasoningCommand = new RelayCommand(() => SelectedReasoning = UiText.T("默认"), () => CanUseSelectedModel);
        OpenProviderSettingsCommand = new RelayCommand(() => ProviderSettingsRequested?.Invoke());
    }

    /// <summary>
    /// 模型选择变化时触发；没有可用模型时两个参数均为 null。
    /// </summary>
    public event Action<AiProvider?, ModelProfile?>? ModelSelected;
    public event Action? ExplicitSelectionChanged;
    public event Action? ProviderSettingsRequested;
    public event Action? PreferenceChanged;
    internal event Func<CancellationToken, Task>? RefreshConversationRequested;

    /// <summary>
    /// 过滤后展示给 UI 的供应商分组集合。
    /// </summary>
    public ObservableCollection<ModelPickerGroupViewModel> FilteredGroups { get; }

    /// <summary>
    /// 选中条目命令。
    /// </summary>
    public IRelayCommand<ModelPickerItemViewModel> SelectItemCommand { get; }

    /// <summary>
    /// 刷新模型与供应商列表命令。
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowModelListCommand { get; }

    public IRelayCommand ShowQuickMenuCommand { get; }

    public IRelayCommand ResetReasoningCommand { get; }
    public IRelayCommand OpenProviderSettingsCommand { get; }

    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set => SetProperty(ref _isPickerOpen, value);
    }

    public bool IsModelListVisible
    {
        get => _isModelListVisible;
        set
        {
            if (SetProperty(ref _isModelListVisible, value))
            {
                OnPropertyChanged(nameof(IsQuickMenuVisible));
            }
        }
    }

    public bool IsQuickMenuVisible => !IsModelListVisible;
    public bool IsLoading => _isLoading;
    public bool HasLoadError => _hasLoadError;
    public bool CanChooseModel => !_disposed && _catalogAvailable && !IsLoading;
    public bool CanUseSelectedModel => CanChooseModel && SelectedItem is { } selected
        && ReferenceEquals(selected, FindItem(selected.Provider.Id, selected.Model.Id));
    public bool HasReasoningError => SelectedItem is { } item
        && !ModelReasoningPolicy.Supports(item.Provider.Protocol, item.Model.ModelId, EffectiveReasoningEffort);
    public bool CanSendWithSelectedModel => CanUseSelectedModel && !HasReasoningError && !_conversationBlocked;
    public string ReasoningNotice => GetLabel("UnsupportedReasoningEffort");
    public IReadOnlyList<string> ReasoningSteps => _reasoningSteps;
    public string ReasoningSliderLabel { get => ReasoningDisplayName; set => SelectedReasoning = value; }
    public bool IsSelectedUnavailable => CanChooseModel && _preferredModelId is not null && SelectedItem is null;
    public bool HasCatalogNotice => _conversationNotice is not null || IsLoading || HasLoadError || IsSelectedUnavailable;
    public string CatalogNotice => _conversationNotice ?? GetLabel(IsLoading ? "LoadingModels"
        : HasLoadError ? "CatalogReloadFailed" : "SelectedModelUnavailable");

    public bool ShowSearch => _allGroups.Count > 1 || _allGroups.Count == 1 && _allGroups[0].Items.Count > 8;

    public bool ShowProviderHeaders => _allGroups.Count > 1;

    public bool HasNoMatches => CanChooseModel && HasModels && FilteredGroups.Count == 0;

    public string EmptyModelTitle => GetLabel(nameof(EmptyModelTitle));
    public string EmptyModelDescription => GetLabel(nameof(EmptyModelDescription));
    public string OpenProviderSettingsLabel => GetLabel(nameof(OpenProviderSettingsLabel));

    public string SelectedModelName => SelectedItem?.DisplayName ?? UiText.T("选择模型");

    /// <summary>
    /// 搜索过滤文本。
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// 当前选中的模型条目。
    /// </summary>
    public ModelPickerItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _reasoningSteps = ReasoningStepsFor(value);
            SetProperty(ref _selectedItem, value);
            OnPropertyChanged(nameof(CurrentDisplayName));
            OnPropertyChanged(nameof(ComposerDisplayName));
            OnPropertyChanged(nameof(ReasoningDisplayName));
            NotifyReasoningState();
            OnPropertyChanged(nameof(SelectedModelName));
            OnPropertyChanged(nameof(EffectiveReasoningEffort));
        }
    }

    /// <summary>
    /// 当前选中的思考强度。
    /// </summary>
    public string SelectedReasoning
    {
        get => _selectedReasoning;
        set
        {
            if (SetProperty(ref _selectedReasoning, value))
            {
                OnPropertyChanged(nameof(EffectiveReasoningEffort));
                OnPropertyChanged(nameof(ReasoningDisplayName));
                NotifyReasoningState();
                if (!_restoringReasoning && !_disposed) PreferenceChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// 当前在触发胶囊上显示的文字。
    /// </summary>
    public string ComposerDisplayName => HasLoadError ? GetLabel("CatalogUnavailableLabel")
        : IsSelectedUnavailable ? GetLabel("SelectedModelUnavailableLabel")
        : SelectedItem?.DisplayName ?? UiText.T("选择模型...");

    public string CurrentDisplayName => !HasLoadError && !IsSelectedUnavailable && SelectedItem is { } selected
        ? $"{selected.DisplayName} · {selected.Provider.Name}"
        : ComposerDisplayName;

    public bool HasModels => _allGroups.Count > 0;
    public bool HasNoModels => !HasModels && !IsLoading && !HasLoadError;

    private static readonly Dictionary<string, string> EffortValues = new(StringComparer.Ordinal)
    {
        [UiText.T("无")] = "none",
        [UiText.T("最低")] = "minimal",
        [UiText.T("低")] = "low",
        [UiText.T("中")] = "medium",
        [UiText.T("高")] = "high",
        [UiText.T("最高")] = "xhigh",
        ["Max"] = "max",
        ["Ultra"] = "ultra"
    };

    public string EffectiveReasoningEffort =>
        EffortValues.TryGetValue(SelectedReasoning, out var effort)
            ? effort
            : SelectedItem?.Model.ReasoningEffort ?? "medium";

    public string ReasoningDisplayName => SelectedItem is null
        ? string.Empty
        : SelectedReasoning != UiText.T("默认")
        ? SelectedReasoning
        : EffortLabel(SelectedItem?.Model.ReasoningEffort);

    private static string EffortLabel(string? effort) => ModelReasoningPolicy.Normalize(effort) switch
    {
        "none" => UiText.T("无"),
        "minimal" => UiText.T("最低"),
        "low" => UiText.T("低"),
        "medium" => UiText.T("中"),
        "high" => UiText.T("高"),
        "xhigh" => UiText.T("最高"),
        "max" => "Max",
        "ultra" => "Ultra",
        _ => UiText.T("默认")
    };

    private void NotifyReasoningState()
    {
        OnPropertyChanged(nameof(ReasoningSteps));
        OnPropertyChanged(nameof(ReasoningSliderLabel));
        OnPropertyChanged(nameof(HasReasoningError));
        OnPropertyChanged(nameof(CanSendWithSelectedModel));
    }

    private static IReadOnlyList<string> ReasoningStepsFor(ModelPickerItemViewModel? item) =>
        ModelReasoningPolicy.SupportedEfforts(item?.Provider.Protocol, item?.Model.ModelId)
            .Select(EffortLabel).ToImmutableArray();

    /// <summary>
    /// 重新从数据库加载所有供应商与其模型。
    /// </summary>
    public Task ReloadAsync() => ReloadWithCancellationAsync(CancellationToken.None);

    private async Task RefreshCatalogAsync(CancellationToken token)
    {
        await ReloadWithCancellationAsync(token);
        if (!_disposed && !token.IsCancellationRequested && RefreshConversationRequested is { } refresh) await refresh(token);
    }

    internal void RestoreConversation(ConversationModelPreference? preference)
    {
        if (_disposed) return;
        _preferredProviderId = preference?.ProviderId;
        _preferredModelId = preference?.ModelProfileId;
        if (CanChooseModel) RestoreOrSelectDefault(_preferredProviderId, _preferredModelId);
        RestoreReasoning(preference?.ReasoningEffort);
    }

    internal void RestoreReasoning(string? effort)
    {
        _restoringReasoning = true;
        try { SelectedReasoning = effort is null ? UiText.T("默认") : EffortLabel(effort); }
        finally { _restoringReasoning = false; }
    }

    internal void SetConversationState(bool blocked, string? noticeKey)
    {
        _conversationBlocked = blocked;
        _conversationNotice = noticeKey is null ? null : GetLabel(noticeKey);
        OnPropertyChanged(nameof(HasCatalogNotice));
        OnPropertyChanged(nameof(CatalogNotice));
        OnPropertyChanged(nameof(CanSendWithSelectedModel));
    }

    internal async Task ReloadWithCancellationAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        var version = ++_loadVersion;
        _readCancellation?.Cancel();
        using var read = _operations.CreateOperation(cancellationToken);
        _readCancellation = read;
        _isLoading = true;
        _catalogAvailable = false;
        _hasLoadError = false;
        NotifyCatalogState();
        try
        {
            var items = await _modelCatalog.ReadAsync(read.Token);
            if (_disposed || version != _loadVersion || read.IsCancellationRequested) return;
            _allGroups.Clear();
            _allGroups.AddRange(items.GroupBy(static item => item.Provider)
                .Select(group => new ModelPickerGroupViewModel(group.Key, group)));
            _catalogAvailable = true;
            _isLoading = false;
            ApplyFilter();
            RestoreOrSelectDefault(_preferredProviderId, _preferredModelId);
        }
        catch (Exception)
        {
            if (_disposed || version != _loadVersion) return;
            _hasLoadError = true;
            IsModelListVisible = true;
        }
        finally
        {
            if (ReferenceEquals(_readCancellation, read)) _readCancellation = null;
            if (!_disposed && version == _loadVersion)
            {
                _isLoading = false;
                NotifyCatalogState();
            }
        }
    }

    /// <summary>
    /// 从外部同步当前活动选中的供应商与模型。
    /// </summary>
    public void SyncSelection(AiProvider? provider, ModelProfile? model)
    {
        if (!CanChooseModel || provider is null || model is null) return;
        if (FindItem(provider.Id, model.Id) is { } current) ApplySelection(current, false);
    }

    public bool TrySelect(Guid providerId, Guid modelId) => TrySelectCore(providerId, modelId, true);

    internal bool TrySelectForAgent(Guid providerId, Guid modelId) => TrySelectCore(providerId, modelId, false);

    private void SelectItem(ModelPickerItemViewModel? item)
    {
        if (item is not null) TrySelectCore(item.Provider.Id, item.Model.Id, true);
    }

    private bool TrySelectCore(Guid providerId, Guid modelId, bool explicitChoice)
    {
        if (!CanChooseModel || FindItem(providerId, modelId) is not { } current) return false;
        IsModelListVisible = false;
        if (explicitChoice) ExplicitSelectionChanged?.Invoke();
        ApplySelection(current, true);
        if (explicitChoice) PreferenceChanged?.Invoke();
        return true;
    }

    private bool CanSelectItem(ModelPickerItemViewModel? item) => CanChooseModel && item is not null
        && FindItem(item.Provider.Id, item.Model.Id) is not null;

    private ModelPickerItemViewModel? FindItem(Guid? providerId, Guid? modelId)
    {
        if (providerId is null || modelId is null) return null;
        foreach (var group in _allGroups)
            for (var index = 0; index < group.Items.Count; index++)
            {
                var item = group.Items[index];
                if (item.Provider.Id == providerId && item.Model.Id == modelId) return item;
            }
        return null;
    }

    private void ApplySelection(ModelPickerItemViewModel current, bool publish)
    {
        _preferredProviderId = current.Provider.Id;
        _preferredModelId = current.Model.Id;
        foreach (var group in _allGroups)
            for (var index = 0; index < group.Items.Count; index++)
                group.Items[index].IsSelected = ReferenceEquals(group.Items[index], current);
        SelectedItem = current;
        if (publish) ModelSelected?.Invoke(current.Provider, current.Model);
        NotifyCatalogState();
    }

    private void ApplyFilter()
    {
        var query = SearchQuery.Trim();
        List<ModelPickerGroupViewModel>? changed = null;
        var count = 0;
        foreach (var group in _allGroups)
        {
            if (group.Filter(query) is not { } filtered) continue;
            if (changed is null && (count >= FilteredGroups.Count || !ReferenceEquals(FilteredGroups[count], filtered)))
            {
                changed = new List<ModelPickerGroupViewModel>(_allGroups.Count);
                for (var index = 0; index < count; index++) changed.Add(FilteredGroups[index]);
            }
            changed?.Add(filtered);
            count++;
        }
        if (changed is not null) FilteredGroups.SynchronizeWith(changed);
        else while (FilteredGroups.Count > count) FilteredGroups.RemoveAt(FilteredGroups.Count - 1);
        OnPropertyChanged(nameof(HasModels));
        OnPropertyChanged(nameof(HasNoModels));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private void RestoreOrSelectDefault(Guid? providerId, Guid? modelId)
    {
        var selected = providerId is not null && modelId is not null
            ? FindItem(providerId, modelId) : FindDefaultItem();
        if (selected is null)
        {
            SelectedItem = null;
            IsModelListVisible = true;
            ModelSelected?.Invoke(null, null);
            return;
        }

        ApplySelection(selected, true);
    }

    private ModelPickerItemViewModel? FindDefaultItem()
    {
        var group = _allGroups.FirstOrDefault(static item => item.Provider.IsDefault)
            ?? _allGroups.FirstOrDefault();
        return group?.Items.FirstOrDefault(static item => item.Model.IsDefault)
            ?? group?.Items.FirstOrDefault();
    }

    private void NotifyCatalogState()
    {
        NotifyReasoningState();
        foreach (var property in new[] { nameof(IsLoading), nameof(HasLoadError), nameof(CanChooseModel),
            nameof(CanUseSelectedModel), nameof(IsSelectedUnavailable), nameof(HasCatalogNotice), nameof(CatalogNotice),
            nameof(CurrentDisplayName), nameof(ComposerDisplayName), nameof(HasNoModels), nameof(HasNoMatches), nameof(ShowSearch), nameof(ShowProviderHeaders) })
            OnPropertyChanged(property);
        SelectItemCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ResetReasoningCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadVersion++;
        _isLoading = false;
        _catalogAvailable = false;
        _operations.Dispose();
        ((GuardedAsyncRelayCommand)RefreshCommand).Dispose();
        ModelSelected = null;
        ExplicitSelectionChanged = null;
        ProviderSettingsRequested = null;
        PreferenceChanged = null;
        RefreshConversationRequested = null;
        NotifyCatalogState();
    }

    private static string GetLabel(string key) =>
        Labels.GetString(key) ?? throw new InvalidOperationException($"Missing model picker resource: {key}");
}

