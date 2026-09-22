using System.Text.Json;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Browser;

internal sealed class EmbeddedBrowserInteractionDriver(
    Func<WebViewControl> getView,
    Func<bool> isDisposed)
{
    private const int MaximumDownloadBytes = 32 * 1024 * 1024;
    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        RunNavigationActionAsync(
            static _ => true,
            view => view.Navigate(uri),
            "浏览器无法导航。",
            cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        RunNavigationActionAsync(
            static view => view.CanGoBack,
            static view => view.GoBack(),
            "当前页面没有可后退的历史记录。",
            cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        RunNavigationActionAsync(
            static view => view.CanGoForward,
            static view => view.GoForward(),
            "当前页面没有可前进的历史记录。",
            cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        RunNavigationActionAsync(
            static _ => true,
            static view => view.Reload(),
            "浏览器无法刷新当前页面。",
            cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        ExecuteStringAsync(EmbeddedBrowserScripts.PageText, cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        ExecuteStringAsync(EmbeddedBrowserScripts.Snapshot, cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        ExecuteAsync(EmbeddedBrowserScripts.Click(selector), cancellationToken);

    public Task TypeAsync(
        string selector,
        string text,
        CancellationToken cancellationToken) =>
        ExecuteAsync(EmbeddedBrowserScripts.Type(selector, text), cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        ExecuteAsync(EmbeddedBrowserScripts.SelectOption(selector, value), cancellationToken);

    public async Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await ExecuteBooleanAsync(
                    EmbeddedBrowserScripts.MatchesState(selector, state.ToString()),
                    cancellationToken))
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException($"等待元素状态超时：{selector} -> {state}");
    }

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        ExecuteAsync(EmbeddedBrowserScripts.Scroll(selector, deltaX, deltaY), cancellationToken);

    public async Task<byte[]> ScreenshotAsync(
        bool fullPage,
        CancellationToken cancellationToken)
    {
        if (fullPage)
        {
            throw new NotSupportedException("内置浏览器当前只支持可见区域截图。");
        }

        var snapshot = await AvaloniaUiThread.RunAsync(
            () => getView().CaptureSnapshotAsync(cancellationToken), cancellationToken);
        return snapshot?.PngData.ToArray()
            ?? throw new InvalidOperationException("内置浏览器未能捕获截图。");
    }

    public async Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            EmbeddedBrowserScripts.StartDownload(selector, MaximumDownloadBytes),
            cancellationToken);
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var json = await ExecuteRawAsync(
                    EmbeddedBrowserScripts.DownloadStatus, cancellationToken);
                using var document = JsonDocument.Parse(json ?? "{}");
                var status = document.RootElement.GetProperty("status").GetString();
                if (status == "ready")
                {
                    var data = document.RootElement.GetProperty("data").GetString() ?? string.Empty;
                    var bytes = Convert.FromBase64String(data);
                    await destination.WriteAsync(bytes, cancellationToken);
                    var suggested = document.RootElement.GetProperty("suggested").GetString();
                    return new BrowserDownloadInfo(suggested ?? "download.bin");
                }

                if (status == "error")
                {
                    var error = document.RootElement.GetProperty("error").GetString();
                    throw new InvalidOperationException($"内置浏览器下载失败：{error}");
                }

                await Task.Delay(100, cancellationToken);
            }

            throw new TimeoutException("等待内置浏览器下载超时。");
        }
        finally
        {
            await ExecuteAsync(EmbeddedBrowserScripts.ClearDownload, CancellationToken.None);
        }
    }

    private async Task RunNavigationActionAsync(
        Func<WebViewControl, bool> canRun,
        Action<WebViewControl> action,
        string unavailableMessage,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(isDisposed(), this);
        var view = getView();
        var completion = new TaskCompletionSource<NativeWebViewNavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<NativeWebViewNavigationCompletedEventArgs>? handler = null;
        handler = (_, args) => completion.TrySetResult(args);
        var subscribed = false;
        try
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (!canRun(view))
                {
                    throw new InvalidOperationException(unavailableMessage);
                }

                view.NavigationCompleted += handler;
                subscribed = true;
                action(view);
            }, cancellationToken);
            var result = await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(60), cancellationToken);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"浏览器导航失败：{result.Error ?? "未知错误"}");
            }
        }
        finally
        {
            if (subscribed)
            {
                await AvaloniaUiThread.RunAsync(
                    () => view.NavigationCompleted -= handler,
                    CancellationToken.None);
            }
        }
    }

    private Task ExecuteAsync(string script, CancellationToken cancellationToken) =>
        AvaloniaUiThread.RunAsync(
            () => getView().ExecuteScriptAsync(script, cancellationToken), cancellationToken);

    private Task<string?> ExecuteRawAsync(string script, CancellationToken cancellationToken) =>
        AvaloniaUiThread.RunAsync(
            () => getView().ExecuteScriptAsync(script, cancellationToken), cancellationToken);

    private async Task<string> ExecuteStringAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var json = await AvaloniaUiThread.RunAsync(
            () => getView().ExecuteScriptAsync(script, cancellationToken), cancellationToken);
        return json is null
            ? string.Empty
            : JsonSerializer.Deserialize<string>(json) ?? string.Empty;
    }

    private async Task<bool> ExecuteBooleanAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var json = await AvaloniaUiThread.RunAsync(
            () => getView().ExecuteScriptAsync(script, cancellationToken), cancellationToken);
        return json is not null && JsonSerializer.Deserialize<bool>(json);
    }
}
