using System.Text.Json;
using MossAgent.Browser;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserDeveloperToolsPipelineTests
{
    [Fact]
    public async Task UnsupportedSession_IsDisabledInUiAndExplicitlyRejectedByTool()
    {
        using var fixture = new BrowserToolbarFixture();
        fixture.Session.SupportsDeveloperTools = false;
        Assert.False(fixture.Pane.DevToolsCommand.CanExecute(null));
        await fixture.RunAsync("devtools");
        Assert.Empty(fixture.Audit.Starts);
        var result = await fixture.Executor.ExecuteAsync(Request("{}"),
            fixture.Context with { BrowserSession = fixture.Session }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("browser_devtools_unsupported", result.ErrorCode);
        Assert.Equal(result.ErrorCode, Assert.Single(fixture.Audit.Completions).ErrorCode);
        Assert.Empty(fixture.Session.Calls);
    }

    [Fact]
    public async Task DefaultSessionImplementation_DoesNotPretendToSupportDevTools()
    {
        using var fixture = new BrowserToolbarFixture();
        var session = new DelayedBrowserSession();
        var result = await fixture.Executor.ExecuteAsync(Request("{}"),
            fixture.Context with { BrowserSession = session }, TestContext.Current.CancellationToken);

        Assert.Equal("browser_devtools_unsupported", result.ErrorCode);
    }

    [Fact]
    public async Task LazySession_ForwardsDevToolsThroughAttachedImplementation()
    {
        using var fixture = new BrowserToolbarFixture();
        var creations = 0;
        await using var lazy = new LazyBrowserSession(_ =>
        {
            creations++;
            return Task.FromResult<MossAgent.Tools.Abstractions.Browser.IBrowserSession>(fixture.Session);
        });
        var result = await fixture.Executor.ExecuteAsync(Request("{}"),
            fixture.Context with { BrowserSession = lazy }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(lazy.SupportsDeveloperTools);
        Assert.Equal(1, creations);
        Assert.Equal("devtools", Assert.Single(fixture.Session.Calls));
    }

    [Fact]
    public async Task InvalidSchema_IsRejectedBeforeOpeningDevTools()
    {
        using var fixture = new BrowserToolbarFixture();
        var result = await fixture.Executor.ExecuteAsync(Request("[]"),
            fixture.Context with { BrowserSession = fixture.Session }, TestContext.Current.CancellationToken);

        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Empty(fixture.Session.Calls);
        Assert.Empty(fixture.ApprovalContexts);
    }

    private static ToolRequest Request(string json) => new("devtools", "browser.devtools", JsonDocument.Parse(json).RootElement.Clone());
}
