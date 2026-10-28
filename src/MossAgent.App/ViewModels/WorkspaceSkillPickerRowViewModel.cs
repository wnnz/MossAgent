using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>List row for one selectable skill component inside a plugin group of the composer
/// skill picker. The bound toggle writes <see cref="IsEnabled"/> before its command runs, so the
/// picker's toggle command persists exactly this value through the task-overrides port (or the
/// unbound-conversation draft) and restores the previous value when the write fails.</summary>
public sealed class WorkspaceSkillPickerRowViewModel : ObservableObject
{
    private bool _isEnabled;

    public WorkspaceSkillPickerRowViewModel(Guid pluginId, PluginComponentDescriptor component, bool isEnabled)
    {
        PluginId = pluginId;
        Component = component;
        _isEnabled = isEnabled;
    }

    public Guid PluginId { get; }
    public PluginComponentDescriptor Component { get; }
    public string Name => Component.Name;
    public string RelativePath => Component.RelativePath;
    public string StateText => SettingsText.Get(IsEnabled ? "PluginStateEnabled" : "PluginStateDisabled");

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value)) OnPropertyChanged(nameof(StateText));
        }
    }
}
