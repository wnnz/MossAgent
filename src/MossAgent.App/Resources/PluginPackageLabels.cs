namespace MossAgent.App.Resources;

public static class PluginPackageLabels
{
    public static string Pick => SettingsText.Get("PluginPick");
    public static string Refresh => SettingsText.Get("PluginRefresh");
    public static string Empty => SettingsText.Get("PluginEmpty");
    public static string EmptyHint => SettingsText.Get("PluginEmptyHint");
    public static string ConfirmTitle => SettingsText.Get("PluginConfirmTitle");
    public static string ConfirmInstall => SettingsText.Get("PluginConfirmInstall");
    public static string CancelInstall => SettingsText.Get("PluginCancelInstall");
    public static string SkillToggleHint => SettingsText.Get("PluginSkillToggleHint");
}
