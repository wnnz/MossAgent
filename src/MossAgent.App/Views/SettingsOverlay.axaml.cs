using Avalonia.Controls;

using MossAgent.App.Controls;
using MossAgent.App.Resources;

namespace MossAgent.App.Views;

/// <summary>
/// 全屏设置遮罩视图，提供集中式的 AI、模型、代理、浏览器及 MCP 选项卡导航。
/// </summary>
public partial class SettingsOverlay : AppUserControl
{
    public string SidebarTitle => SettingsText.Get("SettingsSidebarTitle");
    public AppNavigationPanes Navigation => SettingsNavigation;

    public SettingsOverlay()
    {
        InitializeComponent();
    }
}
