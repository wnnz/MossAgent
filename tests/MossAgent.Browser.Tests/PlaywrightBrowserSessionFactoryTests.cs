using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightBrowserSessionFactoryTests
{
    [Theory]
    [InlineData("http://127.0.0.1:9222/")]
    [InlineData("https://localhost:9222/json/version")]
    [InlineData("ws://[::1]:9222/devtools/browser/test")]
    public void CdpEndpointPolicy_AllowsExplicitLoopback(string endpoint)
    {
        Assert.True(BrowserCdpEndpointPolicy.IsAllowed(new Uri(endpoint)));
    }

    [Theory]
    [InlineData("https://example.com/devtools")]
    [InlineData("http://user:secret@127.0.0.1:9222/")]
    [InlineData("ftp://127.0.0.1:9222/")]
    [InlineData("ws://127.0.0.1.example.com:9222/")]
    public async Task ExistingCdp_RejectsUnauthorizedEndpointBeforeStartingPlaywright(
        string endpoint)
    {
        Assert.False(BrowserCdpEndpointPolicy.IsAllowed(new Uri(endpoint)));
        var options = new BrowserLaunchOptions(
            BrowserEngine.Chrome, BrowserProfileMode.ExistingCdp,
            null, new Uri(endpoint), null, null, null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PlaywrightBrowserSessionFactory().CreateAsync(options, timeout.Token));

        Assert.Contains("本机", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task ExistingCdp_RejectsMissingEndpointBeforeStartingPlaywright()
    {
        Assert.False(BrowserCdpEndpointPolicy.IsAllowed(null));
        var options = new BrowserLaunchOptions(
            BrowserEngine.Edge, BrowserProfileMode.ExistingCdp,
            null, null, null, null, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PlaywrightBrowserSessionFactory().CreateAsync(
                options, TestContext.Current.CancellationToken));

        Assert.Contains("本机", error.Message);
    }
}
