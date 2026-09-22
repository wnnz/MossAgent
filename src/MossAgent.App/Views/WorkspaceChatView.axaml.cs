using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MossAgent.App.Views;

/// <summary>
/// 工作台主任务会话视图，集成顶部模型状态条、中间任务对话流与底部 Composer 输入卡片。
/// </summary>
public partial class WorkspaceChatView : UserControl
{
    public WorkspaceChatView()
    {
        InitializeComponent();
    }

    private void HandleApprovalPolicySelected(object? sender, RoutedEventArgs args) =>
        ApprovalPolicyButton.Flyout?.Hide();
}
