using NativeWebView.Core;
using NativeWebView.Platform.Windows;

namespace MossAgent.App.Tests;

public sealed class NavigationEventBackend : NativeWebViewBackendStubBase
{
    public NavigationEventBackend() : base(NativeWebViewPlatform.Windows, WindowsFeatures()) { }

    private static IWebViewPlatformFeatures WindowsFeatures()
    {
        using var backend = new WindowsNativeWebViewBackend();
        return backend.Features;
    }
}
