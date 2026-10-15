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
        pane.RegisterView(ActiveView);
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
        try
        {
            return await AvaloniaUiThread.RunAsync(() =>
            {
                var id = Guid.NewGuid().ToString("N");
                _views[id] = view;
                pane.RegisterView(view);
                Activate(id, view);
                return id;
            }, cancellationToken);
        }
        catch
        {
            await AvaloniaUiThread.RunAsync(view.Dispose, CancellationToken.None);
            throw;
        }
    }

    public Task SwitchAsync(string tabId, CancellationToken cancellationToken) =>
        AvaloniaUiThread.RunAsync(() =>
        {
            if (!_views.TryGetValue(tabId, out var next))
                throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
            if (tabId != _activeId) Activate(tabId, next);
        }, cancellationToken);

    private void Activate(string tabId, WebViewControl next)
    {
        var previous = ActiveView;
        _activeId = tabId;
        pane.Attach(next);
        ActiveViewChanged?.Invoke(previous, next);
    }

    public Task CloseAsync(string tabId, CancellationToken cancellationToken) =>
        AvaloniaUiThread.RunAsync(() =>
        {
            if (_views.Count <= 1)
                throw new InvalidOperationException("不能关闭浏览器会话的最后一个标签页。");
            if (!_views.TryGetValue(tabId, out var closing))
                throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
            if (tabId == _activeId)
            {
                var nextId = _views.Keys.First(id => id != tabId);
                Activate(nextId, _views[nextId]);
            }
            _views.Remove(tabId);
            pane.UnregisterView(closing);
            closing.Dispose();
        }, cancellationToken);

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
                pane.UnregisterView(view);
                view.Dispose();
            }
        }, CancellationToken.None);
    }
}
