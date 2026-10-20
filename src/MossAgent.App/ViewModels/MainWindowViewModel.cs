using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using System.ComponentModel;
using MossAgent.App.Browser;
using MossAgent.Application.Settings;
using MossAgent.App.Services;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 主窗口视图模型，负责工作台、上下文检查器、全局设置视图及主题模式的生命周期协调。
/// </summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceInspectorViewModel _inspector;
    private readonly IThemeService _themeService;
    private volatile bool _disposed;
    private bool _isSettingsOpen;
    private int _settingsTabIndex = 5;
    private string _settingsSearchQuery = string.Empty;
    /// <summary>左侧项目与会话导航栏的当前显示状态。</summary>
    private bool _isLeftPanelOpen = true;
    private bool _isBottomPanelOpen;

    public MainWindowViewModel(
        WorkspaceViewModel workspace,
        ProviderSettingsViewModel providers,
        ProxySettingsViewModel proxies,
        ModelSettingsViewModel models,
        BrowserSettingsViewModel browser,
        McpSettingsViewModel mcp,
        AgentSettingsViewModel agents,
        SubagentSettingsViewModel subagents,
        TerminalPaneViewModel terminal,
        DiffPaneViewModel diff,
        AuditPaneViewModel audit,
        EmbeddedBrowserWorkspaceViewModel embeddedBrowsers,
        ApprovalViewModel approval,
        IThemeService themeService,
        ThemePaletteSettingsViewModel palette,
        AutomationViewModel? automations = null,
        SourceAccountSettingsViewModel? sourceAccounts = null,
        PullRequestsViewModel? pullRequests = null)
    {
        Workspace = workspace;
        _inspector = new WorkspaceInspectorViewModel(workspace.Catalog);
        _inspector.PropertyChanged += HandleInspectorChanged;
        workspace.FilePreview.Opened += HandlePreviewOpened;
        workspace.FileChanges.ReviewRequested += HandleFileChangesReview;
        Pages = new WorkspacePageNavigationViewModel(workspace.Catalog, automations, pullRequests, workspace.Drafts);
        workspace.Catalog.ConversationRequested += HandleConversationRequested;
        Providers = providers;
        Proxies = proxies;
        SourceAccounts = sourceAccounts;
        Models = models;
        Browser = browser;
        Mcp = mcp;
        Agents = agents;
        Subagents = subagents;
        Terminal = terminal;
        Diff = diff;
        Audit = audit;
        EmbeddedBrowsers = embeddedBrowsers;
        Approval = approval;
        ApprovalTaskControl = new ApprovalTaskControlViewModel(approval, workspace);
        approval.PropertyChanged += HandleApprovalChanged;
        workspace.PropertyChanged += HandleApprovalChanged;
        embeddedBrowsers.Host.IsPresentationBlocked = approval.IsPending;
        _themeService = themeService;
        Palette = palette;
        models.ModelCatalogChanged += ReloadModelCatalogAsync;
        proxies.ProxiesChanged += providers.ReloadProxiesAsync;
        proxies.ProxiesChanged += browser.ReloadProxiesAsync;
        if (sourceAccounts is not null) proxies.ProxiesChanged += sourceAccounts.ReloadProxiesAsync;
        agents.ProfilesChanged += workspace.ReloadAgentsAsync;
        workspace.ModelPicker.ProviderSettingsRequested += HandleProviderSettingsRequested;
        _themeService.ThemeChanged += HandleThemeChanged;

        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        SelectSettingsTabCommand = new RelayCommand<string>(SelectSettingsTab);
        OpenMcpSettingsCommand = new RelayCommand(() => SelectSettingsTab("3"));
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        OpenAutomationsCommand = new RelayCommand(OpenAutomations, () => Pages.OpenAutomationsCommand.CanExecute(null));
        ToggleLeftPanelCommand = new RelayCommand(() => IsLeftPanelOpen = !IsLeftPanelOpen);
        ToggleRightPanelCommand = new RelayCommand(() =>
        {
            IsSettingsOpen = false;
            Pages.IsPullRequestsOpen = false;
            _inspector.ToggleRightPanel();
        });
        ToggleBottomPanelCommand = new RelayCommand(() => IsBottomPanelOpen = !IsBottomPanelOpen);
        ToggleContextCardCommand = new RelayCommand(_inspector.ToggleContextCard);
        ToggleThemeCommand = new RelayCommand(_themeService.ToggleTheme);
        SelectThemeCommand = new RelayCommand<string>(SelectTheme);
        MinimizeWindowCommand = new RelayCommand<Window>(WindowChromeCommands.Minimize);
        ToggleMaximizeWindowCommand = new RelayCommand<Window>(WindowChromeCommands.ToggleMaximize);
        CloseWindowCommand = new RelayCommand<Window>(WindowChromeCommands.Close);
        SelectInspectorTabCommand = new RelayCommand<string>(SelectInspectorTab, InspectorText.IsValidTab);
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
    public SourceAccountSettingsViewModel? SourceAccounts { get; }

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

    public SubagentSettingsViewModel Subagents { get; }
    public AgentSettingsViewModel Agents { get; }
    public WorkspacePageNavigationViewModel Pages { get; }

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
    public EmbeddedBrowserWorkspaceViewModel EmbeddedBrowsers { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _themeService.ThemeChanged -= HandleThemeChanged;
        Workspace.ModelPicker.ProviderSettingsRequested -= HandleProviderSettingsRequested;
        Workspace.Catalog.ConversationRequested -= HandleConversationRequested;
        _inspector.PropertyChanged -= HandleInspectorChanged;
        _inspector.Dispose();
        Pages.Dispose();
        Pages.Automations?.Dispose();
        Diff.Dispose();
        Audit.Dispose();
        Terminal.Dispose();
        Approval.PropertyChanged -= HandleApprovalChanged;
        ApprovalTaskControl.Dispose();
        Workspace.PropertyChanged -= HandleApprovalChanged;
        Workspace.FilePreview.Opened -= HandlePreviewOpened;
        Workspace.FileChanges.ReviewRequested -= HandleFileChangesReview;
        Models.ModelCatalogChanged -= ReloadModelCatalogAsync;
        Proxies.ProxiesChanged -= Providers.ReloadProxiesAsync;
        Proxies.ProxiesChanged -= Browser.ReloadProxiesAsync;
        if (SourceAccounts is not null) Proxies.ProxiesChanged -= SourceAccounts.ReloadProxiesAsync;
        SourceAccounts?.Dispose();
        Proxies.Dispose();
        Browser.Dispose();
        Agents.ProfilesChanged -= Workspace.ReloadAgentsAsync;
        Agents.Dispose();
        Subagents.Dispose();
    }

    private async Task ReloadModelCatalogAsync()
    {
        await Workspace.ModelPicker.ReloadAsync();
        await Subagents.ReloadModelsAsync();
        await Agents.ReloadModelsAsync();
        await Workspace.ReloadAgentsAsync();
    }

    private void HandleApprovalChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || (args.PropertyName != nameof(ApprovalViewModel.IsPending)
            && args.PropertyName != nameof(WorkspaceViewModel.IsShuttingDown))) return;
        _ = AvaloniaUiThread.RunAsync(() =>
        {
            if (!_disposed) EmbeddedBrowsers.Host.IsPresentationBlocked = Approval.IsPending || Workspace.IsShuttingDown;
        }, CancellationToken.None);
    }

    /// <summary>
    /// 工具执行审批视图模型。
    /// </summary>
    public ApprovalViewModel Approval { get; }
    public ApprovalTaskControlViewModel ApprovalTaskControl { get; }

    /// <summary>
    /// 是否处于暗黑模式。
    /// </summary>
    public bool IsDarkMode => _themeService.IsDarkMode;

    /// <summary>
    /// 是否打开全屏/模态设置抽屉。
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            if (!SetProperty(ref _isSettingsOpen, value)) return;
            if (value) Pages.IsPullRequestsOpen = false;
        }
    }

    public int SettingsTabIndex
    {
        get => _settingsTabIndex;
        set
        {
            if (!SetProperty(ref _settingsTabIndex, value)) return;
            OnPropertyChanged(nameof(IsProviderSettingsSelected));
            OnPropertyChanged(nameof(IsProxySettingsSelected));
            OnPropertyChanged(nameof(IsBrowserSettingsSelected));
            OnPropertyChanged(nameof(IsMcpSettingsSelected));
            OnPropertyChanged(nameof(IsSubagentSettingsSelected));
            OnPropertyChanged(nameof(IsAgentSettingsSelected));
            OnPropertyChanged(nameof(IsAppearanceSettingsSelected));
            OnPropertyChanged(nameof(IsSourceAccountSettingsSelected));
        }
    }

    public bool IsProviderSettingsSelected => SettingsTabIndex == 0;

    public bool IsProxySettingsSelected => SettingsTabIndex == 1;

    public bool IsBrowserSettingsSelected => SettingsTabIndex == 2;

    public bool IsMcpSettingsSelected => SettingsTabIndex == 3;

    public bool IsSubagentSettingsSelected => SettingsTabIndex == 4;
    public bool IsAgentSettingsSelected => SettingsTabIndex == 6;

    public bool IsAppearanceSettingsSelected => SettingsTabIndex == 5;
    public bool IsSourceAccountSettingsSelected => SettingsTabIndex == 7 && SourceAccounts is not null;

    public bool IsSystemThemeSelected => _themeService.Preference == ThemePreference.System;
    public bool IsLightThemeSelected => _themeService.Preference == ThemePreference.Light;
    public bool IsDarkThemeSelected => _themeService.Preference == ThemePreference.Dark;

    public ThemePaletteSettingsViewModel Palette { get; }

    public ThemePaletteEditorViewModel ActivePalette => IsDarkMode ? Palette.Dark : Palette.Light;

    public string? AppearanceError => _themeService.PersistenceError;

    public bool HasAppearanceError => !string.IsNullOrWhiteSpace(AppearanceError);

    public string SettingsSearchQuery
    {
        get => _settingsSearchQuery;
        set
        {
            if (!SetProperty(ref _settingsSearchQuery, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(IsProviderSettingsMatch));
            OnPropertyChanged(nameof(IsProxySettingsMatch));
            OnPropertyChanged(nameof(IsBrowserSettingsMatch));
            OnPropertyChanged(nameof(IsMcpSettingsMatch));
            OnPropertyChanged(nameof(IsSubagentSettingsMatch));
            OnPropertyChanged(nameof(IsAgentSettingsMatch));
            OnPropertyChanged(nameof(IsAppearanceSettingsMatch));
            OnPropertyChanged(nameof(IsSourceAccountSettingsMatch));
            OnPropertyChanged(nameof(IsPersonalSettingsMatch));
            OnPropertyChanged(nameof(IsIntegrationSettingsMatch));
            OnPropertyChanged(nameof(IsAgentSectionSettingsMatch));
            OnPropertyChanged(nameof(HasSettingsSearchResults));
        }
    }

    public bool IsProviderSettingsMatch => MatchesSettingsQuery("AI 提供商 提供商与模型 供应商 模型");
    public bool IsProxySettingsMatch => MatchesSettingsQuery("网络代理 Proxy SOCKS");
    public bool IsBrowserSettingsMatch => MatchesSettingsQuery("浏览器 Browser CDP");
    public bool IsMcpSettingsMatch => MatchesSettingsQuery("插件 MCP 扩展");
    public bool IsSubagentSettingsMatch => MatchesSettingsQuery("子代理 模型 思考");
    public bool IsAgentSettingsMatch => MatchesSettingsQuery("自定义代理 提示词 模型 思考");
    public bool IsAppearanceSettingsMatch => MatchesSettingsQuery("外观 主题 浅色 深色 系统");
    public bool IsSourceAccountSettingsMatch => SourceAccounts is not null
        && MatchesSettingsQuery(SourceAccountText.Title + " GitHub PR Pull Request source account");
    public bool IsPersonalSettingsMatch => IsAppearanceSettingsMatch || IsProviderSettingsMatch || IsProxySettingsMatch;
    public bool IsIntegrationSettingsMatch => IsMcpSettingsMatch || IsBrowserSettingsMatch || IsSourceAccountSettingsMatch;
    public bool IsAgentSectionSettingsMatch => IsAgentSettingsMatch || IsSubagentSettingsMatch;
    public bool HasSettingsSearchResults => IsProviderSettingsMatch || IsProxySettingsMatch
        || IsBrowserSettingsMatch || IsMcpSettingsMatch || IsSubagentSettingsMatch
        || IsAppearanceSettingsMatch || IsAgentSettingsMatch || IsSourceAccountSettingsMatch;

    /// <summary>
    /// 是否展开左侧项目和会话导航栏。
    /// </summary>
    public bool IsLeftPanelOpen
    {
        get => _isLeftPanelOpen;
        set => SetProperty(ref _isLeftPanelOpen, value);
    }

    /// <summary>
    /// 是否展开右侧上下文检查器面板（Diff/终端/审计/浏览器）。
    /// </summary>
    public bool IsRightPanelOpen
    {
        get => _inspector.IsRightPanelOpen;
        set => _inspector.IsRightPanelOpen = value;
    }

    public bool IsContextCardVisible => _inspector.IsContextCardVisible;

    public string RightPanelToggleLabel => _inspector.RightPanelToggleLabel;

    public Thickness ChatMessageViewportMargin => _inspector.ChatMessageViewportMargin;

    internal void SetChatViewportWidth(double width) => _inspector.SetChatViewportWidth(width);

    internal void SetContextCardHeight(double height) => _inspector.SetContextCardHeight(height);

    public IRelayCommand ToggleContextCardCommand { get; }

    public bool IsBottomPanelOpen
    {
        get => _isBottomPanelOpen;
        set => SetProperty(ref _isBottomPanelOpen, value);
    }

    public IRelayCommand ToggleBottomPanelCommand { get; }

    /// <summary>
    /// 打开全局设置视图的命令。
    /// </summary>
    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand ToggleSettingsCommand { get; }

    public IRelayCommand<string> SelectSettingsTabCommand { get; }

    public IRelayCommand OpenMcpSettingsCommand { get; }

    /// <summary>
    /// 关闭全局设置视图的命令。
    /// </summary>
    public IRelayCommand CloseSettingsCommand { get; }

    public IRelayCommand OpenAutomationsCommand { get; }

    /// <summary>
    /// 切换左侧项目和会话导航栏显示状态的命令。
    /// </summary>
    public IRelayCommand ToggleLeftPanelCommand { get; }

    /// <summary>
    /// 切换右侧面板显示/隐藏的命令。
    /// </summary>
    public IRelayCommand ToggleRightPanelCommand { get; }

    /// <summary>
    /// 切换明亮/暗黑主题的命令。
    /// </summary>
    public IRelayCommand ToggleThemeCommand { get; }

    public IRelayCommand<string> SelectThemeCommand { get; }

    public IRelayCommand<Window> MinimizeWindowCommand { get; }

    public IRelayCommand<Window> ToggleMaximizeWindowCommand { get; }

    public IRelayCommand<Window> CloseWindowCommand { get; }

    public string ActiveInspectorTab
    {
        get => _inspector.ActiveInspectorTab;
        set => _inspector.ActiveInspectorTab = value;
    }

    public bool IsInspectorHome => _inspector.IsInspectorHome;
    public string InspectorTitle => _inspector.InspectorTitle;
    public bool IsInspectorDiff => _inspector.IsInspectorDiff;
    public bool IsInspectorTerminal => _inspector.IsInspectorTerminal;
    public bool IsInspectorBrowser => _inspector.IsInspectorBrowser;
    public bool IsInspectorAudit => _inspector.IsInspectorAudit;
    public bool IsInspectorSources => _inspector.IsInspectorSources;

    public IRelayCommand<string> SelectInspectorTabCommand { get; }

    public IRelayCommand BackToInspectorHomeCommand { get; }

    private void HandleProviderSettingsRequested()
    {
        if (!_disposed) SelectSettingsTab("0");
    }

    private void HandleConversationRequested()
    {
        if (!_disposed) IsSettingsOpen = false;
    }

    private void HandleThemeChanged()
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(ActivePalette));
        OnPropertyChanged(nameof(IsSystemThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(AppearanceError));
        OnPropertyChanged(nameof(HasAppearanceError));
    }

    private void SelectSettingsTab(string? tab)
    {
        if (!int.TryParse(tab, out var index) || index is < 0 or > 7 || (index == 7 && SourceAccounts is null)) return;
        Workspace.ModelPicker.IsPickerOpen = false;
        SettingsTabIndex = index;
        IsSettingsOpen = true;
    }

    private void SelectTheme(string? theme)
    {
        if (Enum.TryParse<ThemePreference>(theme, true, out var preference)
            && Enum.IsDefined(preference))
        {
            _themeService.SetPreference(preference);
        }
    }

    private bool MatchesSettingsQuery(string terms)
    {
        var query = SettingsSearchQuery.Trim();
        return query.Length == 0 || terms.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void SelectInspectorTab(string? tab)
    {
        if (!InspectorText.IsValidTab(tab)) return;
        IsSettingsOpen = false;
        Pages.IsAutomationsOpen = false;
        Pages.IsPullRequestsOpen = false;
        ActiveInspectorTab = tab!;
        Workspace.FilePreview.Hide();
        IsRightPanelOpen = true;
    }

    private void OpenAutomations()
    {
        if (!Pages.OpenAutomationsCommand.CanExecute(null)) return;
        IsSettingsOpen = false;
        Pages.OpenAutomationsCommand.Execute(null);
    }

    private void HandleInspectorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_disposed) OnPropertyChanged(args.PropertyName);
    }

    private void HandlePreviewOpened()
    {
        if (_disposed) return;
        Pages.IsAutomationsOpen = false;
        IsRightPanelOpen = true;
    }

    private void HandleFileChangesReview()
    {
        if (!_disposed) SelectInspectorTab("diff");
    }
}
