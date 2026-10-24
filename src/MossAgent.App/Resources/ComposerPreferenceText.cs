using MossAgent.App.ViewModels;

namespace MossAgent.App.Resources;

public static class ComposerPreferenceText
{
    public static string Title => WorkspaceText.Get("ComposerSettingsTitle");
    public static string Description => WorkspaceText.Get("ComposerSettingsDescription");
    public static string DefaultAction => WorkspaceText.Get("ComposerDefaultAction");
    public static string QueueFirst => WorkspaceText.Get("ComposerQueueFirst");
    public static string SteerFirst => WorkspaceText.Get("ComposerSteerFirst");
    public static string Save => WorkspaceText.Get("ComposerSettingsSave");
    public static string Reload => WorkspaceText.Get("ComposerSettingsReload");
    public static string LoadFailed => WorkspaceText.Get("ComposerSettingsLoadFailed");
    public static string SaveFailed => WorkspaceText.Get("ComposerSettingsSaveFailed");
    public static string QueueFirstHint => WorkspaceText.Get("ComposerQueueFirstHint");
    public static string SteerFirstHint => WorkspaceText.Get("ComposerSteerFirstHint");
}
