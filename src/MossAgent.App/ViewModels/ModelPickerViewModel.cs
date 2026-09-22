using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 模型选择器弹窗视图模型，提供按供应商分组的模型检索、选择及思考强度快速切换。
/// </summary>
public sealed class ModelPickerViewModel : ObservableObject
{
    private readonly IConfigurationRepository _configurations;
    private readonly List<ModelPickerGroupViewModel> _allGroups = [];
    private string _searchQuery = string.Empty;
    private ModelPickerItemViewModel? _selectedItem;
    private string _selectedReasoning = "关闭";
    private bool _isNetSearchEnabled = true;

    public ModelPickerViewModel(IConfigurationRepository configurations)
    {
        _configurations = configurations;
        FilteredGroups = [];
        ReasoningOptions = ["关闭", "最低", "低", "中", "高"];
        SelectItemCommand = new RelayCommand<ModelPickerItemViewModel>(SelectItem);
        ToggleNetSearchCommand = new RelayCommand(() => IsNetSearchEnabled = !IsNetSearchEnabled);
        RefreshCommand = new AsyncRelayCommand(ReloadAsync);
    }

    /// <summary>
    /// 模型被用户选中时触发的回调事件（通知外部更新 Provider 与 Model）。
    /// </summary>
    public event Action<AiProvider, ModelProfile>? ModelSelected;

    /// <summary>
    /// 过滤后展示给 UI 的供应商分组集合。
    /// </summary>
    public ObservableCollection<ModelPickerGroupViewModel> FilteredGroups { get; }

    /// <summary>
    /// 思考强度选项列表。
    /// </summary>
    public IReadOnlyList<string> ReasoningOptions { get; }

    /// <summary>
    /// 选中条目命令。
    /// </summary>
    public IRelayCommand<ModelPickerItemViewModel> SelectItemCommand { get; }

    /// <summary>
    /// 联网搜索状态切换命令。
    /// </summary>
    public IRelayCommand ToggleNetSearchCommand { get; }

    /// <summary>
    /// 刷新模型与供应商列表命令。
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

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
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(CurrentDisplayName));
            }
        }
    }

    /// <summary>
    /// 当前选中的思考强度。
    /// </summary>
    public string SelectedReasoning
    {
        get => _selectedReasoning;
        set => SetProperty(ref _selectedReasoning, value);
    }

    /// <summary>
    /// 是否开启网络工具调用。
    /// </summary>
    public bool IsNetSearchEnabled
    {
        get => _isNetSearchEnabled;
        set => SetProperty(ref _isNetSearchEnabled, value);
    }

    /// <summary>
    /// 当前在触发胶囊上显示的文字。
    /// </summary>
    public string CurrentDisplayName => SelectedItem is not null
        ? $"{SelectedItem.DisplayName} · {SelectedItem.Provider.Name}"
        : "选择模型...";

    /// <summary>
    /// 重新从数据库加载所有供应商与其模型。
    /// </summary>
    public async Task ReloadAsync()
    {
        _allGroups.Clear();
        var providers = await _configurations.GetProvidersAsync(CancellationToken.None);
        foreach (var provider in providers.Where(p => p.IsEnabled))
        {
            var models = await _configurations.GetModelsAsync(provider.Id, CancellationToken.None);
            var enabledModels = models.Where(m => m.IsEnabled).ToList();
            if (enabledModels.Count == 0) continue;

            var items = enabledModels.Select(m => new ModelPickerItemViewModel(provider, m)).ToList();
            _allGroups.Add(new ModelPickerGroupViewModel(provider, items));
        }

        ApplyFilter();
    }

    /// <summary>
    /// 从外部同步当前活动选中的供应商与模型。
    /// </summary>
    public void SyncSelection(AiProvider? provider, ModelProfile? model)
    {
        if (provider is null || model is null) return;

        foreach (var group in _allGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsSelected = item.Provider.Id == provider.Id && item.Model.Id == model.Id;
                if (item.IsSelected)
                {
                    SelectedItem = item;
                }
            }
        }
    }

    private void SelectItem(ModelPickerItemViewModel? item)
    {
        if (item is null) return;
        SelectedItem = item;
        SyncSelection(item.Provider, item.Model);
        ModelSelected?.Invoke(item.Provider, item.Model);
    }

    private void ApplyFilter()
    {
        FilteredGroups.Clear();
        var query = SearchQuery.Trim();

        foreach (var group in _allGroups)
        {
            if (string.IsNullOrEmpty(query))
            {
                FilteredGroups.Add(group);
                continue;
            }

            var matches = group.Items
                .Where(i => i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || i.ModelId.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || group.ProviderName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 0)
            {
                FilteredGroups.Add(new ModelPickerGroupViewModel(group.Provider, matches));
            }
        }
    }
}
