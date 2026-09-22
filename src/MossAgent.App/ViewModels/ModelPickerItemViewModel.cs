using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 模型选择器条目视图模型，对应单一可用模型及其归属供应商。
/// </summary>
public sealed class ModelPickerItemViewModel : ObservableObject
{
    private bool _isSelected;

    public ModelPickerItemViewModel(AiProvider provider, ModelProfile model)
    {
        Provider = provider;
        Model = model;
    }

    /// <summary>
    /// 模型归属的 AI 供应商。
    /// </summary>
    public AiProvider Provider { get; }

    /// <summary>
    /// 模型配置档案。
    /// </summary>
    public ModelProfile Model { get; }

    /// <summary>
    /// 模型显示名称。
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Model.DisplayName) ? Model.ModelId : Model.DisplayName;

    /// <summary>
    /// 模型标识。
    /// </summary>
    public string ModelId => Model.ModelId;

    /// <summary>
    /// 是否已被当前任务或工作区选中。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
