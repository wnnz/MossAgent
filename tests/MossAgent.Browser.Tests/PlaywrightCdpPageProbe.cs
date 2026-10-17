using Microsoft.Playwright;

namespace MossAgent.Browser.Tests;

internal sealed class PlaywrightCdpPageProbe
{
    private EventHandler<IPage>? _popup;

    public PlaywrightCdpPageProbe(string name)
    {
        Name = name;
        Page = PlaywrightApiProxy.Create<IPage>((method, arguments) => method.Name switch
        {
            "get_IsClosed" => IsClosed,
            "get_Url" => Url,
            "TitleAsync" => Task.FromResult(Name),
            "GotoAsync" => Navigate((string)arguments![0]!),
            "BringToFrontAsync" => Task.CompletedTask,
            "CloseAsync" => Close(),
            "add_Popup" => Subscribe((EventHandler<IPage>)arguments![0]!),
            "remove_Popup" => Unsubscribe((EventHandler<IPage>)arguments![0]!),
            _ => throw new NotSupportedException(method.Name)
        });
    }

    public string Name { get; }
    public IPage Page { get; }
    public string Url { get; private set; } = "about:blank";
    public bool IsClosed { get; private set; }
    public int CloseCount { get; private set; }
    public int SubscriptionCount => _popup?.GetInvocationList().Length ?? 0;
    public Exception? CloseFailure { get; set; }
    public Action? OnClose { get; set; }

    public void OpenPopup(PlaywrightCdpPageProbe popup) => _popup?.Invoke(Page, popup.Page);
    public EventHandler<IPage>? CapturePopupHandler() => _popup;

    private Task<IResponse?> Navigate(string url)
    {
        Url = url;
        return Task.FromResult<IResponse?>(null);
    }

    private Task Close()
    {
        CloseCount++;
        OnClose?.Invoke();
        if (CloseFailure is not null) return Task.FromException(CloseFailure);
        IsClosed = true;
        return Task.CompletedTask;
    }

    private object? Subscribe(EventHandler<IPage> handler) { _popup += handler; return null; }
    private object? Unsubscribe(EventHandler<IPage> handler) { _popup -= handler; return null; }
}
