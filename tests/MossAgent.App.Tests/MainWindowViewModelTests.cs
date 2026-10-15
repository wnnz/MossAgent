using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
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
        Assert.NotSame(viewModel.Palette.Light, viewModel.Palette.Dark);
        Assert.True(viewModel.IsLeftPanelOpen);
        Assert.False(viewModel.IsRightPanelOpen);
        Assert.True(viewModel.IsContextCardVisible);
        Assert.NotNull(viewModel.Workspace);
        Assert.NotNull(viewModel.Providers);
        Assert.NotNull(viewModel.Models);
        Assert.NotNull(viewModel.Proxies);
        Assert.NotNull(viewModel.Browser);
        Assert.NotNull(viewModel.Mcp);
        Assert.NotNull(viewModel.Terminal);
        Assert.NotNull(viewModel.Diff);
        Assert.NotNull(viewModel.Audit);
        Assert.NotNull(viewModel.EmbeddedBrowsers.CurrentPane);
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

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.True(viewModel.IsSettingsOpen);
        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task OpenMcpSettingsCommand_SelectsPluginSettings()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.OpenMcpSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsOpen);
        Assert.Equal(3, viewModel.SettingsTabIndex);
    }

    [Fact]
    public async Task EmptyModelPicker_NavigatesToProvidersAndClosesFlyout()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();
        var picker = viewModel.Workspace.ModelPicker;
        picker.IsPickerOpen = true;

        picker.OpenProviderSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsOpen);
        Assert.True(viewModel.IsProviderSettingsSelected);
        Assert.False(picker.IsPickerOpen);
    }

    [Fact]
    public async Task SelectSettingsTabCommand_OpensRequestedSettingsPage()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.SelectSettingsTabCommand.Execute("2");

        Assert.True(viewModel.IsSettingsOpen);
        Assert.Equal(2, viewModel.SettingsTabIndex);
        Assert.True(viewModel.IsBrowserSettingsSelected);
    }

    [Fact]
    public async Task SettingsSearchQuery_FiltersAvailablePages()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.SettingsSearchQuery = "浏览器";

        Assert.True(viewModel.IsBrowserSettingsMatch);
        Assert.False(viewModel.IsProviderSettingsMatch);
        Assert.True(viewModel.HasSettingsSearchResults);

        viewModel.SettingsSearchQuery = "AI 提供商";
        Assert.True(viewModel.IsProviderSettingsMatch);
        Assert.True(viewModel.HasSettingsSearchResults);

        viewModel.SettingsSearchQuery = "不存在的页面";
        Assert.False(viewModel.HasSettingsSearchResults);
    }

    [Fact]
    public async Task AppearancePage_CanBeSelectedAndSearched()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.SettingsSearchQuery = "主题";
        Assert.True(viewModel.IsAppearanceSettingsMatch);
        Assert.False(viewModel.IsBrowserSettingsMatch);

        viewModel.SelectSettingsTabCommand.Execute("5");
        Assert.True(viewModel.IsAppearanceSettingsSelected);
        Assert.True(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task AgentPage_CanBeSelectedAndSearched()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.SettingsSearchQuery = "自定义代理";
        Assert.True(viewModel.IsAgentSettingsMatch);
        Assert.False(viewModel.IsSubagentSettingsMatch);

        viewModel.SelectSettingsTabCommand.Execute("6");
        Assert.True(viewModel.IsAgentSettingsSelected);
        Assert.True(viewModel.IsSettingsOpen);
    }

    [Fact]
    public async Task SelectThemeCommand_AppliesOnlyKnownThemes()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-theme-command-test", Guid.NewGuid().ToString("N"));
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var viewModel = services.GetRequiredService<MainWindowViewModel>();

            viewModel.SelectThemeCommand.Execute("Dark");
            Assert.True(viewModel.IsDarkThemeSelected);
            Assert.True(viewModel.IsDarkMode);

            viewModel.SelectThemeCommand.Execute("not-a-theme");
            Assert.True(viewModel.IsDarkThemeSelected);

            viewModel.SelectThemeCommand.Execute("Light");
            Assert.True(viewModel.IsLightThemeSelected);
            Assert.False(viewModel.IsDarkMode);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SelectThemeCommand_ReportsPersistenceFailureAndRecovers()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-theme-command-test", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var settingsPath = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(settingsPath, "not-json");
            await using var services = AppComposition.CreateServices(root);
            var viewModel = services.GetRequiredService<MainWindowViewModel>();

            viewModel.SelectThemeCommand.Execute("Dark");
            Assert.True(viewModel.IsDarkThemeSelected);
            Assert.True(viewModel.HasAppearanceError);
            Assert.Contains("保存失败", viewModel.AppearanceError);

            File.WriteAllText(settingsPath, "{}");
            viewModel.SelectThemeCommand.Execute("Light");
            Assert.True(viewModel.IsLightThemeSelected);
            Assert.False(viewModel.HasAppearanceError);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ThemeCodeFontSelection_PersistsThroughAppearanceStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-font-command-test", Guid.NewGuid().ToString("N"));
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var viewModel = services.GetRequiredService<MainWindowViewModel>();
            var editor = viewModel.Palette.Light.Fonts;
            var option = Assert.Single(editor.CodeFontOptions,
                item => item.Value == CodeFontPreference.CascadiaCode);

            editor.SelectedCodeFontOption = option;

            Assert.Equal(option, editor.SelectedCodeFontOption);
            Assert.Equal(CodeFontPreference.CascadiaCode,
                services.GetRequiredService<IAppearancePreferenceStore>().LoadCodeFont(ThemePreference.Light));
            Assert.False(editor.HasError);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 验证顶部侧栏按钮可以折叠并重新显示项目导航。
    /// </summary>
    [Fact]
    public async Task ToggleLeftPanelCommand_TogglesProjectNavigationVisibility()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.ToggleLeftPanelCommand.Execute(null);
        Assert.False(viewModel.IsLeftPanelOpen);

        viewModel.ToggleLeftPanelCommand.Execute(null);
        Assert.True(viewModel.IsLeftPanelOpen);
    }

    [Fact]
    public async Task ToggleRightPanelCommand_TogglesInspectionPanelVisibility()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        Assert.False(viewModel.IsRightPanelOpen);

        viewModel.ToggleRightPanelCommand.Execute(null);
        Assert.True(viewModel.IsRightPanelOpen);
        Assert.False(viewModel.IsContextCardVisible);
        Assert.True(viewModel.IsInspectorSources);

        viewModel.ToggleRightPanelCommand.Execute(null);
        Assert.False(viewModel.IsRightPanelOpen);
        Assert.True(viewModel.IsContextCardVisible);
    }

    [Fact]
    public async Task ToggleContextCardCommand_HidesCardAndRestoresItFromInspector()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.ToggleContextCardCommand.Execute(null);
        Assert.False(viewModel.IsContextCardVisible);

        viewModel.ToggleContextCardCommand.Execute(null);
        Assert.True(viewModel.IsContextCardVisible);

        viewModel.SelectInspectorTabCommand.Execute("terminal");
        Assert.False(viewModel.IsContextCardVisible);

        viewModel.ToggleContextCardCommand.Execute(null);
        Assert.False(viewModel.IsRightPanelOpen);
        Assert.True(viewModel.IsContextCardVisible);
    }

    [Fact]
    public async Task ContextCardInset_AppliesOnlyWhenMessagesCouldOverlap()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        Assert.Equal(new Thickness(0), viewModel.ChatMessageViewportMargin);

        viewModel.SetContextCardHeight(196);
        Assert.Equal(new Thickness(0, 218, 0, 0), viewModel.ChatMessageViewportMargin);

        viewModel.SetChatViewportWidth(1604);
        Assert.Equal(new Thickness(0), viewModel.ChatMessageViewportMargin);

        viewModel.SetChatViewportWidth(1420);
        Assert.Equal(new Thickness(0), viewModel.ChatMessageViewportMargin);

        viewModel.SetChatViewportWidth(1419);
        Assert.Equal(new Thickness(0, 218, 0, 0), viewModel.ChatMessageViewportMargin);

        viewModel.SetContextCardHeight(300);
        Assert.Equal(new Thickness(0, 322, 0, 0), viewModel.ChatMessageViewportMargin);

        viewModel.SetChatViewportWidth(double.NaN);
        Assert.Equal(new Thickness(0, 322, 0, 0), viewModel.ChatMessageViewportMargin);

        viewModel.SetContextCardHeight(196);

        viewModel.ToggleContextCardCommand.Execute(null);
        Assert.Equal(new Thickness(0), viewModel.ChatMessageViewportMargin);

        viewModel.ToggleContextCardCommand.Execute(null);
        Assert.Equal(new Thickness(0, 218, 0, 0), viewModel.ChatMessageViewportMargin);

        viewModel.IsRightPanelOpen = true;
        Assert.Equal(new Thickness(0), viewModel.ChatMessageViewportMargin);

        viewModel.SetContextCardHeight(double.NaN);
        viewModel.IsRightPanelOpen = false;
        Assert.Equal(new Thickness(0, 218, 0, 0), viewModel.ChatMessageViewportMargin);
    }

    [Fact]
    public async Task BottomPanelAndSourcesView_OpenIndependently()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        viewModel.ToggleBottomPanelCommand.Execute(null);
        Assert.True(viewModel.IsBottomPanelOpen);
        Assert.True(viewModel.IsContextCardVisible);

        viewModel.SelectInspectorTabCommand.Execute("sources");
        Assert.True(viewModel.IsInspectorSources);
        Assert.True(viewModel.IsRightPanelOpen);
        Assert.False(viewModel.IsContextCardVisible);

        viewModel.ToggleBottomPanelCommand.Execute(null);
        Assert.False(viewModel.IsBottomPanelOpen);

        viewModel.BackToInspectorHomeCommand.Execute(null);
        Assert.True(viewModel.IsInspectorHome);
        Assert.True(viewModel.IsInspectorSources);
    }

    [Fact]
    public async Task SelectInspectorTabCommand_OpensRequestedPanel()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();
        viewModel.IsRightPanelOpen = false;

        viewModel.SelectInspectorTabCommand.Execute("terminal");

        Assert.True(viewModel.IsRightPanelOpen);
        Assert.True(viewModel.IsInspectorTerminal);
    }

    [Fact]
    public async Task ToggleThemeCommand_SwitchesThemeAndRestoresOriginalState()
    {
        await using var services = AppComposition.CreateServices();
        var viewModel = services.GetRequiredService<MainWindowViewModel>();

        var initial = viewModel.IsDarkMode;

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.NotEqual(initial, viewModel.IsDarkMode);
        Assert.Equal(viewModel.IsDarkMode, viewModel.IsDarkThemeSelected);
        Assert.Equal(!viewModel.IsDarkMode, viewModel.IsLightThemeSelected);

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.Equal(initial, viewModel.IsDarkMode);
    }
}
