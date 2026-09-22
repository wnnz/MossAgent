using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Browser;

internal sealed class EmbeddedBrowserTabSet(
    WebViewControl initialView,
    EmbeddedBrowserPaneViewModel pane,
    Func<CancellationToken, Task<WebViewControl>> createView) : IAsyncDisposable
{
    private readonly Dictionary<string, WebViewControl> _views =
        new() { [Guid.NewGuid().ToString("N")] = initialView };
    private string _activeId = string.Empty;

    public event Action<WebViewControl?, WebViewControl>? ActiveViewChanged;

    public WebViewControl ActiveView => _views[_activeId];

    public void Initialize()
    {
        _activeId = _views.Keys.Single();
        pane.Attach(ActiveView);
    }

    public async Task<IReadOnlyList<BrowserTabInfo>> ListAsync(
        CancellationToken cancellationToken)
    {
        var tabs = new List<BrowserTabInfo>();
        foreach (var pair in _views)
        {
            var json = await AvaloniaUiThread.RunAsync(
                () => pair.Value.ExecuteScriptAsync("document.title || ''", cancellationToken),
                cancellationToken);
            var title = json is null
                ? string.Empty
                : JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            tabs.Add(new BrowserTabInfo(
                pair.Key, title, pair.Value.CurrentUrl, pair.Key == _activeId));
        }

        return tabs;
    }

    public async Task<string> NewAsync(CancellationToken cancellationToken)
    {
        var view = await createView(cancellationToken);
        var id = Guid.NewGuid().ToString("N");
        _views[id] = view;
        await SwitchAsync(id, cancellationToken);
        return id;
    }

    public async Task SwitchAsync(string tabId, CancellationToken cancellationToken)
    {
        if (!_views.TryGetValue(tabId, out var next))
        {
            throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
        }

        if (tabId == _activeId)
        {
            return;
        }

        var previous = ActiveView;
        _activeId = tabId;
        await AvaloniaUiThread.RunAsync(() => pane.Attach(next), cancellationToken);
        ActiveViewChanged?.Invoke(previous, next);
    }

    public async Task CloseAsync(string tabId, CancellationToken cancellationToken)
    {
        if (_views.Count <= 1)
        {
            throw new InvalidOperationException("不能关闭浏览器会话的最后一个标签页。");
        }

        if (!_views.TryGetValue(tabId, out var closing))
        {
            throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
        }

        if (tabId == _activeId)
        {
            await SwitchAsync(_views.Keys.First(id => id != tabId), cancellationToken);
        }

        _views.Remove(tabId);
        await AvaloniaUiThread.RunAsync(closing.Dispose, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        var activeView = ActiveView;
        var views = _views.Values.ToArray();
        _views.Clear();
        await AvaloniaUiThread.RunAsync(() =>
        {
            pane.Detach(activeView);
            foreach (var view in views)
            {
                view.Dispose();
            }
        }, CancellationToken.None);
    }
}
