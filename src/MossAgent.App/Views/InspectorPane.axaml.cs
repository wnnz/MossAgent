using Avalonia.Controls;
using MossAgent.App.ViewModels;

using MossAgent.App.Controls;

namespace MossAgent.App.Views;

/// <summary>
/// 右侧上下文检查器面板，集成代码变更 Diff、交互终端、工具审计与内置浏览器。
/// </summary>
public partial class InspectorPane : AppUserControl
{
    public InspectorPane()
    {
        InitializeComponent();
    }

    private void HandleContextCardSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetContextCardHeight(args.NewSize.Height);
        }
    }
}
