using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EmbeddedBrowserTabRefreshTests
{
    [Fact]
    public async Task DetachedSession_LateRefreshCannotRestoreTabs()
    {
        var pane = new EmbeddedBrowserPaneViewModel();
        var session = new DelayedBrowserSession();
        pane.AttachSession(session);
        session.Requests.Dequeue().SetResult([]);
        var refresh = pane.RefreshTabsAsync();
        pane.DetachSession(session);
        session.Requests.Dequeue().SetResult(Tabs("old"));
        await CompleteAsync(refresh);

        Assert.Empty(pane.Tabs);
        Assert.Null(pane.SelectedTab);
        Assert.False(pane.CloseTabCommand.CanExecute(null));
    }

    [Fact]
    public async Task OlderRefresh_CannotReplaceNewerTabs()
    {
        var pane = new EmbeddedBrowserPaneViewModel();
        var session = new DelayedBrowserSession();
        pane.AttachSession(session);
        session.Requests.Dequeue().SetResult([]);
        var older = pane.RefreshTabsAsync();
        var olderResult = session.Requests.Dequeue();
        var newer = pane.RefreshTabsAsync();
        session.Requests.Dequeue().SetResult(Tabs("new"));
        await CompleteAsync(newer);
        olderResult.SetResult(Tabs("old"));
        await CompleteAsync(older);

        Assert.Equal("new", Assert.Single(pane.Tabs).Id);
        Assert.Equal("new", pane.SelectedTab?.Id);
    }

    [Fact]
    public async Task ReplacedSession_LateFailureCannotOverrideStatus()
    {
        var pane = new EmbeddedBrowserPaneViewModel();
        var previous = new DelayedBrowserSession();
        pane.AttachSession(previous);
        previous.Requests.Dequeue().SetResult([]);
        var oldRefresh = pane.RefreshTabsAsync();
        var current = new DelayedBrowserSession();
        pane.AttachSession(current);
        current.Requests.Dequeue().SetResult(Tabs("current"));
        var status = pane.Status;
        previous.Requests.Dequeue().SetException(new IOException("synthetic-secret"));
        await CompleteAsync(oldRefresh);

        Assert.Equal(status, pane.Status);
        Assert.DoesNotContain("synthetic-secret", pane.Status);
    }

    [Fact]
    public async Task CurrentRefreshFailure_UsesSafeStatus()
    {
        var pane = new EmbeddedBrowserPaneViewModel();
        var session = new DelayedBrowserSession();
        pane.AttachSession(session);
        session.Requests.Dequeue().SetResult([]);
        var refresh = pane.RefreshTabsAsync();
        session.Requests.Dequeue().SetException(new IOException("synthetic-secret"));
        await CompleteAsync(refresh);

        Assert.Contains("刷新标签页失败", pane.Status);
        Assert.DoesNotContain("synthetic-secret", pane.Status);
    }

    private static IReadOnlyList<BrowserTabInfo> Tabs(string id) =>
        [new(id, id, null, true)];

    private static Task CompleteAsync(Task work) =>
        work.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
}
