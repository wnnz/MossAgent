using System.Text.Json;
using MossAgent.Browser.Tools;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class BrowserInteractionToolsTests
{
    [Fact]
    public async Task HistoryTools_InvokeBoundSession()
    {
        var session = new StubBrowserSession();
        var context = Context(session);

        var cancellationToken = TestContext.Current.CancellationToken;
        await new BrowserBackTool().ExecuteAsync(
            Request("browser.back", "{}"), context, cancellationToken);
        await new BrowserForwardTool().ExecuteAsync(
            Request("browser.forward", "{}"), context, cancellationToken);
        await new BrowserReloadTool().ExecuteAsync(
            Request("browser.reload", "{}"), context, cancellationToken);

        Assert.Equal(1, session.BackCount);
        Assert.Equal(1, session.ForwardCount);
        Assert.Equal(1, session.ReloadCount);
    }

    [Fact]
    public async Task WaitSelectAndScroll_ForwardValidatedArguments()
    {
        var session = new StubBrowserSession();
        var context = Context(session);
        var cancellationToken = TestContext.Current.CancellationToken;

        await new BrowserWaitTool().ExecuteAsync(
            Request("browser.wait", """{"selector":"#ready","state":"attached","timeoutMs":1500}"""),
            context, cancellationToken);
        Assert.Equal("#ready", session.LastSelector);
        Assert.Equal(BrowserElementState.Attached, session.LastWaitState);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), session.LastTimeout);

        await new BrowserSelectTool().ExecuteAsync(
            Request("browser.select", """{"selector":"#country","value":"sg"}"""),
            context, cancellationToken);
        Assert.Equal("#country", session.LastSelector);
        Assert.Equal("sg", session.LastValue);

        await new BrowserScrollTool().ExecuteAsync(
            Request("browser.scroll", """{"selector":".list","deltaX":10,"deltaY":250}"""),
            context, cancellationToken);
        Assert.Equal(".list", session.LastSelector);
        Assert.Equal(10, session.LastDeltaX);
        Assert.Equal(250, session.LastDeltaY);
    }

    [Fact]
    public async Task WaitTool_RejectsUnknownState()
    {
        var session = new StubBrowserSession();
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await new BrowserWaitTool().ExecuteAsync(
            Request("browser.wait", """{"selector":"#ready","state":"unknown"}"""),
            Context(session), cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_state", result.ErrorCode);
        Assert.Null(session.LastWaitState);
    }

    [Fact]
    public async Task TabTools_CreateListSwitchAndCloseTabs()
    {
        var session = new StubBrowserSession();
        var context = Context(session);
        var cancellationToken = TestContext.Current.CancellationToken;

        var created = await new BrowserNewTabTool().ExecuteAsync(
            Request("browser.new_tab", """{"url":"https://example.com/docs"}"""),
            context,
            cancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Equal("tab-2", session.ActiveTabId);
        Assert.Equal(new Uri("https://example.com/docs"), session.CurrentUri);

        var listed = await new BrowserTabsTool().ExecuteAsync(
            Request("browser.tabs", "{}"), context, cancellationToken);
        var tabs = JsonSerializer.Deserialize<BrowserTabInfo[]>(listed.Content!);
        Assert.Equal(2, tabs!.Length);
        Assert.True(tabs.Single(static tab => tab.Id == "tab-2").IsActive);

        await new BrowserSwitchTabTool().ExecuteAsync(
            Request("browser.switch_tab", """{"tabId":"tab-1"}"""),
            context,
            cancellationToken);
        Assert.Equal("tab-1", session.ActiveTabId);

        await new BrowserCloseTabTool().ExecuteAsync(
            Request("browser.close_tab", """{"tabId":"tab-2"}"""),
            context,
            cancellationToken);
        Assert.Equal(1, session.TabCount);
    }

    [Fact]
    public async Task CloseTabTool_RejectsClosingLastTab()
    {
        var session = new StubBrowserSession();
        var tool = new BrowserCloseTabTool();

        var action = () => tool.ExecuteAsync(
            Request("browser.close_tab", """{"tabId":"tab-1"}"""),
            Context(session),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(1, session.TabCount);
    }

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com/archive")]
    public async Task NavigateTool_RejectsNonHttpProtocols(string url)
    {
        var session = new StubBrowserSession();
        var result = await new BrowserNavigateTool().ExecuteAsync(
            Request("browser.navigate", JsonSerializer.Serialize(new { url })),
            Context(session),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_url", result.ErrorCode);
        Assert.Null(session.CurrentUri);
    }

    [Fact]
    public async Task NewTabTool_RejectsNonHttpProtocolBeforeCreatingTab()
    {
        var session = new StubBrowserSession();
        var result = await new BrowserNewTabTool().ExecuteAsync(
            Request("browser.new_tab", """{"url":"file:///C:/secret.txt"}"""),
            Context(session),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_url", result.ErrorCode);
        Assert.Equal(1, session.TabCount);
    }

    [Fact]
    public async Task DownloadTool_SavesBoundedTaskArtifact()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "MossAgent.Browser.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var session = new StubBrowserSession();
            var context = new ToolExecutionContext(
                Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess,
                ArtifactDirectory: root,
                BrowserSession: session);

            var result = await new BrowserDownloadTool().ExecuteAsync(
                Request("browser.download", """{"selector":"#download"}"""),
                context,
                TestContext.Current.CancellationToken);

            var artifact = Assert.Single(result.Artifacts!);
            Assert.True(result.IsSuccess);
            Assert.Equal("#download", session.LastSelector);
            Assert.Equal("report.txt", artifact.Name);
            Assert.Equal("text/plain", artifact.MediaType);
            Assert.StartsWith(Path.GetFullPath(root), artifact.Path, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(artifact.Path));
            Assert.NotEmpty(artifact.Sha256);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ToolRequest Request(string name, string json) => new(
        Guid.NewGuid().ToString("N"), name, JsonDocument.Parse(json).RootElement.Clone());

    private static ToolExecutionContext Context(IBrowserSession session) => new(
        Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
        ApprovalPolicy.FullAccess, BrowserSession: session);
}
