using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 侧栏中的单个任务条目，保存任务数据及当前会话高亮状态。
/// </summary>
public sealed class WorkspaceTaskNavigationViewModel(AgentTask task) : ObservableObject
{
    /// <summary>此任务是否为当前打开的会话。</summary>
    private bool _isSelected;

    /// <summary>
    /// 对应的持久化任务，点击条目时用于打开会话。
    /// </summary>
    public AgentTask Task { get; } = task;

    public string PinToggleLabel => SettingsText.Get(Task.IsPinned ? "UnpinTask" : "PinTask");
    public bool IsRunning => Task.Status == AgentTaskStatus.Running;

    /// <summary>
    /// 此条目是否对应当前打开的任务。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
