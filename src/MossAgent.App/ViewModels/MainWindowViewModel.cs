namespace MossAgent.App.ViewModels;

public sealed class MainWindowViewModel
{
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
        ApprovalViewModel approval)
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
    }

    public WorkspaceViewModel Workspace { get; }
    public ProviderSettingsViewModel Providers { get; }
    public ProxySettingsViewModel Proxies { get; }
    public ModelSettingsViewModel Models { get; }
    public BrowserSettingsViewModel Browser { get; }
    public McpSettingsViewModel Mcp { get; }
    public TerminalPaneViewModel Terminal { get; }
    public DiffPaneViewModel Diff { get; }
    public AuditPaneViewModel Audit { get; }
    public EmbeddedBrowserPaneViewModel EmbeddedBrowser { get; }
    public ApprovalViewModel Approval { get; }
}
