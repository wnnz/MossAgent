using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Browser.Tools;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.App.Tests;

internal sealed class BrowserToolbarFixture : IDisposable
{
    public BrowserToolbarFixture(ApprovalPolicy policy = ApprovalPolicy.FullAccess, TimeSpan? timeout = null)
    {
        Context = new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], policy);
        Executor = new(new ToolRegistry(Tools()), new PolicyApprovalService(async (_, _, context, token) =>
        {
            ApprovalContexts.Add(context);
            return await Approve(token);
        }), Audit);
        Pane = new(executor: Executor, taskId: Context.TaskId, actionTimeout: timeout);
        View = new(Backend);
        Pane.Attach(View);
        Pane.AttachContext(Context);
        Pane.AttachSession(Session);
        Backend.Navigate(new Uri("https://fixture.invalid/first"));
        Backend.Navigate(new Uri("https://fixture.invalid/second"));
        Backend.Navigate(new Uri("https://fixture.invalid/third"));
        Backend.GoBack();
    }

    public ToolExecutionContext Context { get; }
    public ToolExecutor Executor { get; }
    public BrowserToolbarAuditSink Audit { get; } = new();
    public BrowserToolbarSession Session { get; } = new();
    public NavigationEventBackend Backend { get; } = new();
    public AppEmbeddedBrowserView View { get; }
    public EmbeddedBrowserPaneViewModel Pane { get; }
    public List<ToolExecutionContext> ApprovalContexts { get; } = [];
    public Func<CancellationToken, Task<bool>> Approve { get; set; } = _ => Task.FromResult(true);

    public static IAgentTool[] Tools() => [new BrowserNavigateTool(), new BrowserBackTool(), new BrowserForwardTool(),
        new BrowserReloadTool(), new BrowserNewTabTool(), new BrowserCloseTabTool(), new BrowserSwitchTabTool(), new BrowserDevToolsTool()];

    public Task RunAsync(string action) => action switch
    {
        "navigate" => Pane.NavigateCommand.ExecuteAsync(null),
        "back" => Pane.BackCommand.ExecuteAsync(null),
        "forward" => Pane.ForwardCommand.ExecuteAsync(null),
        "reload" => Pane.ReloadCommand.ExecuteAsync(null),
        "new_tab" => Pane.NewTabCommand.ExecuteAsync(null),
        "close_tab" => Pane.CloseTabCommand.ExecuteAsync(null),
        "switch_tab" => Pane.SwitchTabCommand.ExecuteAsync(Pane.Tabs.Single(tab => !tab.IsActive)),
        "devtools" => Pane.DevToolsCommand.ExecuteAsync(null),
        _ => throw new ArgumentException("Unknown toolbar action.", nameof(action))
    };

    public void Dispose()
    {
        Pane.DetachSession(Session);
        Pane.Detach(View);
        Pane.Dispose();
        View.Dispose();
        Backend.Dispose();
    }
}
