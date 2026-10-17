namespace MossAgent.App.Resources;

public static class McpSettingsLabels
{
    public static string Reconnect => SettingsText.Get("McpReconnect");
    public static string CancelReconnect => SettingsText.Get("McpCancelReconnect");
    public static string ServerName => SettingsText.Get("McpServerName");
    public static string Transport => SettingsText.Get("McpTransport");
    public static string Command => SettingsText.Get("McpCommand");
    public static string Arguments => SettingsText.Get("McpArguments");
    public static string WorkingDirectory => SettingsText.Get("McpWorkingDirectory");
    public static string Environment => SettingsText.Get("McpEnvironment");
    public static string Url => SettingsText.Get("McpUrl");
    public static string Headers => SettingsText.Get("McpHeaders");
    public static string Proxy => SettingsText.Get("McpProxy");
}
