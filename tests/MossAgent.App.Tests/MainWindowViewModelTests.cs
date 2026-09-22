using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

/// <summary>
/// 主窗口视图模型单元测试，验证设置抽屉、右侧检查器面板及主题切换的状态流转与命令执行。
/// </summary>
public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task InitialState_HasExpectedDefaults()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        Assert.False(viewModel.IsSettingsOpen);
        Assert.True(viewModel.IsRightPanelOpen);
        Assert.Equal(viewModel.IsDarkMode ? "☾" : "☼", viewModel.ThemeIcon);
        Assert.NotNull(viewModel.Workspace);
        Assert.NotNull(viewModel.Providers);
        Assert.NotNull(viewModel.Models);
        Assert.NotNull(viewModel.Proxies);
        Assert.NotNull(viewModel.Browser);
        Assert.NotNull(viewModel.Mcp);
        Assert.NotNull(viewModel.Terminal);
        Assert.NotNull(viewModel.Diff);
        Assert.NotNull(viewModel.Audit);
        Assert.NotNull(viewModel.EmbeddedBrowser);
        Assert.NotNull(viewModel.Approval);
    }

    [Fact]
    public async Task OpenAndCloseSettingsCommand_TogglesSettingsStateCorrectly()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        Assert.False(viewModel.IsSettingsOpen);

        viewModel.OpenSettingsCommand.Execute(null);
        Assert.True(viewModel.IsSettingsOpen);

        viewModel.CloseSettingsCommand.Execute(null);
        Assert.False(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task ToggleRightPanelCommand_TogglesInspectionPanelVisibility()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        Assert.True(viewModel.IsRightPanelOpen);

        viewModel.ToggleRightPanelCommand.Execute(null);
        Assert.False(viewModel.IsRightPanelOpen);

        viewModel.ToggleRightPanelCommand.Execute(null);
        Assert.True(viewModel.IsRightPanelOpen);
    }

    [Fact]
    public async Task ToggleThemeCommand_SwitchesThemeAndUpdatesIcon()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        var initial = viewModel.IsDarkMode;
        var initialIcon = viewModel.ThemeIcon;

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.NotEqual(initial, viewModel.IsDarkMode);
        Assert.NotEqual(initialIcon, viewModel.ThemeIcon);

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.Equal(initial, viewModel.IsDarkMode);
        Assert.Equal(initialIcon, viewModel.ThemeIcon);
    }
}
