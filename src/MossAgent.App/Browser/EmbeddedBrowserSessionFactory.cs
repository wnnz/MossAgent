using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Browser;

public sealed class EmbeddedBrowserSessionFactory(
    EmbeddedBrowserPaneViewModel pane) : IEmbeddedBrowserSessionFactory
{
    public Task<IBrowserSession> CreateAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Engine != BrowserEngine.Embedded)
        {
            throw new ArgumentException("内置浏览器工厂只接受 Embedded 引擎。", nameof(options));
        }

        if (options.ProfileMode == BrowserProfileMode.ExistingCdp)
        {
            throw new InvalidOperationException("内置浏览器不支持连接已有 CDP 会话。");
        }

        return AvaloniaUiThread.RunAsync<IBrowserSession>(
            () => CreateOnUiThreadAsync(options, cancellationToken), cancellationToken);
    }

    private async Task<IBrowserSession> CreateOnUiThreadAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        var view = await CreateViewOnUiThreadAsync(options, cancellationToken);
        return new EmbeddedBrowserSession(
            view,
            pane,
            token => AvaloniaUiThread.RunAsync(
                () => CreateViewOnUiThreadAsync(options, token), token));
    }

    private static async Task<WebViewControl> CreateViewOnUiThreadAsync(
        BrowserLaunchOptions options,
        CancellationToken cancellationToken)
    {
        NativeWebViewRuntime.EnsureCurrentPlatformRegistered();
        if (!NativeWebViewRuntime.Factory.TryCreateNativeWebViewBackend(
                NativeWebViewRuntime.CurrentPlatform, out var backend))
        {
            throw new PlatformNotSupportedException("当前平台没有可用的内置浏览器后端。");
        }

        var view = new WebViewControl(backend);
        Configure(view, options);

        try
        {
            await view.InitializeAsync(cancellationToken);
            view.RenderMode = NativeWebViewRenderMode.Embedded;
            return view;
        }
        catch
        {
            view.Dispose();
            throw;
        }
    }

    private static void Configure(
        WebViewControl view,
        BrowserLaunchOptions options)
    {
        var environment = view.InstanceConfiguration.EnvironmentOptions;
        environment.UserDataFolder = options.ProfileDirectory;
        environment.Proxy = options.ProxyUri is null
            ? new NativeWebViewProxyOptions { NoProxy = true }
            : new NativeWebViewProxyOptions { Server = options.ProxyUri.ToString() };

        var controller = view.InstanceConfiguration.ControllerOptions;
        controller.ProfileName = options.ProfileMode == BrowserProfileMode.DailyProfile
            ? "Default"
            : "MossAgent";
        controller.IsJavaScriptEnabled = true;
        view.IsDevToolsEnabled = true;
    }
}
