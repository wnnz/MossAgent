using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class BrowserToolbarText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(BrowserToolbarText).Assembly);

    public static string AddressLabel => Get("BrowserAddressLabel");
    public static string NavigateLabel => Get("BrowserNavigateLabel");
    public static string BackLabel => Get("BrowserBackLabel");
    public static string ForwardLabel => Get("BrowserForwardLabel");
    public static string ReloadLabel => Get("BrowserReloadLabel");
    public static string DevToolsLabel => Get("BrowserDevToolsLabel");
    public static string CancelActionLabel => Get("BrowserCancelActionLabel");

    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing workspace resource: {key}");
}
