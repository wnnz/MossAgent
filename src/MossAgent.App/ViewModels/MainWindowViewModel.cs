using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 主窗口视图模型，负责工作台、上下文检查器、全局设置视图及主题模式的生命周期协调。
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private bool _isSettingsOpen;
    private bool _isRightPanelOpen = true;
    private string _activeInspectorTab = "home";

    public MainWindowViewModel(
        WorkspaceViewModel workspace,
        ProviderSettingsViewModel providers,
        ProxySettingsViewModel proxies,
        ModelSettingsViewModel models,
        BrowserSettingsViewModel browser,
        McpSettingsViewModel mcp,
        TerminalPaneViewModel terminal,
        DiffPaneViewModel diff,
        AuditPaneViewModel audit,
        EmbeddedBrowserPaneViewModel embeddedBrowser,
        ApprovalViewModel approval,
        IThemeService themeService)
    {
        Workspace = workspace;
        Providers = providers;
        Proxies = proxies;
        Models = models;
        Browser = browser;
        Mcp = mcp;
        Terminal = terminal;
        Diff = diff;
        Audit = audit;
        EmbeddedBrowser = embeddedBrowser;
        Approval = approval;
        _themeService = themeService;

        _themeService.ThemeChanged += () =>
        {
            OnPropertyChanged(nameof(IsDarkMode));
            OnPropertyChanged(nameof(ThemeIcon));
        };

        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        ToggleRightPanelCommand = new RelayCommand(() => IsRightPanelOpen = !IsRightPanelOpen);
        ToggleThemeCommand = new RelayCommand(_themeService.ToggleTheme);
        MinimizeWindowCommand = new RelayCommand<Window>(MinimizeWindow);
        ToggleMaximizeWindowCommand = new RelayCommand<Window>(ToggleMaximizeWindow);
        CloseWindowCommand = new RelayCommand<Window>(CloseWindow);
        SelectInspectorTabCommand = new RelayCommand<string>(tab => ActiveInspectorTab = tab ?? "home");
        BackToInspectorHomeCommand = new RelayCommand(() => ActiveInspectorTab = "home");
    }

    /// <summary>
    /// 核心工作区视图模型（包含项目、会话历史、消息流与输入）。
    /// </summary>
    public WorkspaceViewModel Workspace { get; }

    /// <summary>
    /// AI 供应商配置视图模型。
    /// </summary>
    public ProviderSettingsViewModel Providers { get; }

    /// <summary>
    /// 网络代理配置视图模型。
    /// </summary>
    public ProxySettingsViewModel Proxies { get; }

    /// <summary>
    /// 模型配置视图模型。
    /// </summary>
    public ModelSettingsViewModel Models { get; }

    /// <summary>
    /// 浏览器配置视图模型。
    /// </summary>
    public BrowserSettingsViewModel Browser { get; }

    /// <summary>
    /// MCP 扩展服务配置视图模型。
    /// </summary>
    public McpSettingsViewModel Mcp { get; }

    /// <summary>
    /// 终端面板视图模型。
    /// </summary>
    public TerminalPaneViewModel Terminal { get; }

    /// <summary>
    /// 代码变更对比面板视图模型。
    /// </summary>
    public DiffPaneViewModel Diff { get; }

    /// <summary>
    /// 审计与产物面板视图模型。
    /// </summary>
    public AuditPaneViewModel Audit { get; }

    /// <summary>
    /// 内置 WebView2 浏览器视图模型。
    /// </summary>
    public EmbeddedBrowserPaneViewModel EmbeddedBrowser { get; }

    /// <summary>
    /// 工具执行审批视图模型。
    /// </summary>
    public ApprovalViewModel Approval { get; }

    /// <summary>
    /// 是否处于暗黑模式。
    /// </summary>
    public bool IsDarkMode => _themeService.IsDarkMode;

    /// <summary>
    /// 主题切换图标（暗黑模式显示月亮，明亮模式显示太阳）。
    /// </summary>
    public string ThemeIcon => IsDarkMode ? "☾" : "☼";

    /// <summary>
    /// 是否打开全屏/模态设置抽屉。
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    /// <summary>
    /// 是否展开右侧上下文检查器面板（Diff/终端/审计/浏览器）。
    /// </summary>
    public bool IsRightPanelOpen
    {
        get => _isRightPanelOpen;
        set => SetProperty(ref _isRightPanelOpen, value);
    }

    /// <summary>
    /// 打开全局设置视图的命令。
    /// </summary>
    public IRelayCommand OpenSettingsCommand { get; }

    /// <summary>
    /// 关闭全局设置视图的命令。
    /// </summary>
    public IRelayCommand CloseSettingsCommand { get; }

    /// <summary>
    /// 切换右侧面板显示/隐藏的命令。
    /// </summary>
    public IRelayCommand ToggleRightPanelCommand { get; }

    /// <summary>
    /// 切换明亮/暗黑主题的命令。
    /// </summary>
    public IRelayCommand ToggleThemeCommand { get; }

    public IRelayCommand<Window> MinimizeWindowCommand { get; }

    public IRelayCommand<Window> ToggleMaximizeWindowCommand { get; }

    public IRelayCommand<Window> CloseWindowCommand { get; }

    /// <summary>
    /// 当前检查器面板激活的视图类型（home, diff, terminal, browser, audit）。
    /// </summary>
    public string ActiveInspectorTab
    {
        get => _activeInspectorTab;
        set
        {
            if (SetProperty(ref _activeInspectorTab, value))
            {
                OnPropertyChanged(nameof(IsInspectorHome));
                OnPropertyChanged(nameof(IsInspectorDiff));
                OnPropertyChanged(nameof(IsInspectorTerminal));
                OnPropertyChanged(nameof(IsInspectorBrowser));
                OnPropertyChanged(nameof(IsInspectorAudit));
            }
        }
    }

    public bool IsInspectorHome => ActiveInspectorTab == "home";
    public bool IsInspectorDiff => ActiveInspectorTab == "diff";
    public bool IsInspectorTerminal => ActiveInspectorTab == "terminal";
    public bool IsInspectorBrowser => ActiveInspectorTab == "browser";
    public bool IsInspectorAudit => ActiveInspectorTab == "audit";

    /// <summary>
    /// 切换检查器子视图命令。
    /// </summary>
    public IRelayCommand<string> SelectInspectorTabCommand { get; }

    /// <summary>
    /// 返回检查器主页命令。
    /// </summary>
    public IRelayCommand BackToInspectorHomeCommand { get; }

    private static void MinimizeWindow(Window? window)
    {
        if (window is not null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private static void ToggleMaximizeWindow(Window? window)
    {
        if (window is not null)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private static void CloseWindow(Window? window) => window?.Close();
}
