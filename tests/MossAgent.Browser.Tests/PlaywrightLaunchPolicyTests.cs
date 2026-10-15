using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightLaunchPolicyTests
{
    [Theory]
    [InlineData(BrowserEngine.Chrome, "chrome")]
    [InlineData(BrowserEngine.Edge, "msedge")]
    public void ExternalManagedBrowser_PreservesChromiumSandbox(BrowserEngine engine, string channel)
    {
        var options = PlaywrightBrowserSessionFactory.CreateLaunchOptions(
            new BrowserLaunchOptions(engine, BrowserProfileMode.Managed, "synthetic-profile", null, null, null, null));

        Assert.True(options.ChromiumSandbox);
        Assert.False(options.Headless);
        Assert.Equal(channel, options.Channel);
        Assert.Null(options.Proxy);
        Assert.True(options.Args is null || !options.Args.Contains("--no-sandbox"));
    }
}
