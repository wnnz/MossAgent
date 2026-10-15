using MossAgent.Application.Settings;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed record UiFontOption(UiFontPreference Value, string Label)
{
    public string CompactLabel => Value == UiFontPreference.Default ? SettingsText.Get("UiFontSystemCompact") : Label;
}
