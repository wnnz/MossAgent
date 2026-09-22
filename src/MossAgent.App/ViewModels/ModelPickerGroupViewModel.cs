using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 模型选择器分组视图模型，按 AI 供应商聚合所属模型列表。
/// </summary>
public sealed class ModelPickerGroupViewModel : ObservableObject
{
    public ModelPickerGroupViewModel(AiProvider provider, IEnumerable<ModelPickerItemViewModel> items)
    {
        Provider = provider;
        Items = new ObservableCollection<ModelPickerItemViewModel>(items);
    }

    /// <summary>
    /// 当前分组对应的 AI 供应商。
    /// </summary>
    public AiProvider Provider { get; }

    /// <summary>
    /// 供应商名称。
    /// </summary>
    public string ProviderName => Provider.Name;

    /// <summary>
    /// 该供应商下挂载的模型条目集合。
    /// </summary>
    public ObservableCollection<ModelPickerItemViewModel> Items { get; }
}
