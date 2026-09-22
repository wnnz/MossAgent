using Avalonia.Controls;

namespace MossAgent.App.Views;

/// <summary>
/// MCP (Model Context Protocol) 扩展配置视图，支持外部 stdio 及 HTTP MCP 工具注册与诊断。
/// </summary>
public partial class McpSettingsPane : UserControl
{
    public McpSettingsPane()
    {
        InitializeComponent();
    }
}
