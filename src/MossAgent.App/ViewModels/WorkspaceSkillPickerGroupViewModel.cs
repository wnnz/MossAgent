using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>One enabled plugin's skill section in the composer skill picker: a header line of
/// name plus version over the plugin's selectable skill rows.</summary>
public sealed class WorkspaceSkillPickerGroupViewModel
{
    public WorkspaceSkillPickerGroupViewModel(InstalledPlugin plugin,
        IReadOnlyList<WorkspaceSkillPickerRowViewModel> items)
    {
        Plugin = plugin;
        Items = items;
    }

    public InstalledPlugin Plugin { get; }
    public IReadOnlyList<WorkspaceSkillPickerRowViewModel> Items { get; }
    public string Header => $"{Plugin.Name} v{Plugin.Version}";
}
