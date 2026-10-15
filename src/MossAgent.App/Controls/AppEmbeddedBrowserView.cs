using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Controls;

public sealed class AppEmbeddedBrowserView(INativeWebViewBackend backend) : WebViewControl(backend);
