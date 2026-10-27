using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>List row for one installed plugin package. <see cref="Update"/> re-points the row
/// after a lifecycle transition; <see cref="IsUninstallPending"/> drives the two-stage
/// uninstall label on the shared settings item row. <see cref="Skills"/> carries one row per
/// skill component so each can be enabled or disabled independently.</summary>
public sealed class InstalledPluginRowViewModel : ObservableObject
{
    private bool _uninstallPending;

    public InstalledPluginRowViewModel(InstalledPlugin plugin,
        IEnumerable<PluginSkillRowViewModel>? skills = null)
    {
        Plugin = plugin;
        if (skills is null) return;
        foreach (var skill in skills) Skills.Add(skill);
    }

    public InstalledPlugin Plugin { get; private set; }
    public Guid Id => Plugin.Id;
    public string Name => Plugin.Name;
    public string Version => Plugin.Version;
    public string InstallPath => Plugin.InstallPath;
    public bool IsEnabled => Plugin.State == InstalledPluginState.Enabled;
    public ObservableCollection<PluginSkillRowViewModel> Skills { get; } = [];
    public bool HasSkills => Skills.Count > 0;

    public string StateText => Plugin.State switch
    {
        InstalledPluginState.Enabled => SettingsText.Get("PluginStateEnabled"),
        InstalledPluginState.Disabled => SettingsText.Get("PluginStateDisabled"),
        _ => SettingsText.Get("PluginStateInstalled")
    };

    public string Subtitle => string.Format(CultureInfo.CurrentCulture,
        SettingsText.Get("PluginRowSummary"), Plugin.Version, Plugin.Components.Count, Plugin.InstallPath);

    public string UninstallLabel => _uninstallPending
        ? SettingsText.Get("PluginUninstallConfirm")
        : SettingsText.Get("PluginUninstall");

    public bool IsUninstallPending
    {
        get => _uninstallPending;
        set
        {
            if (SetProperty(ref _uninstallPending, value)) OnPropertyChanged(nameof(UninstallLabel));
        }
    }

    public void Update(InstalledPlugin plugin)
    {
        Plugin = plugin;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(InstallPath));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(Subtitle));
    }
}
