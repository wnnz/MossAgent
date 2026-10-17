namespace MossAgent.App.Resources;

public static class ProjectEditorText
{
    public static string Name => SettingsText.Get("ProjectName");
    public static string Directories => SettingsText.Get("ProjectSourceDirectories");
    public static string EmptyDirectories => SettingsText.Get("ProjectEmptyDirectories");
    public static string Primary => SettingsText.Get("ProjectPrimaryDirectory");
    public static string SetPrimary => SettingsText.Get("ProjectSetPrimaryDirectory");
    public static string RemoveDirectory => SettingsText.Get("ProjectRemoveDirectory");
    public static string AddDirectory => SettingsText.Get("ProjectAddDirectory");
    public static string Cancel => SettingsText.Get("ProjectCancelEditing");
}
