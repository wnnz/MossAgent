using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class StreamableHttpMcpClientTests
{
    [Fact]
    public async Task Client_InitializesListsAndCallsToolAgainstLocalServer()
    {
        await using var server = new McpHttpFixtureServer();
        using var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        httpClient.DefaultRequestHeaders.Add("X-Moss-Fixture", "enabled");
        var profile = new McpServerProfile(
            Guid.NewGuid(), "fixture", McpTransportKind.StreamableHttp,
            null, "[]", null, "{}", server.Endpoint,
            "{\"X-Moss-Fixture\":\"enabled\"}", null, true);
        var client = new StreamableHttpMcpClient(profile, httpClient);
        var cancellationToken = TestContext.Current.CancellationToken;

        await client.InitializeAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken);
        var result = await client.CallToolAsync(
            "echo",
            JsonSerializer.SerializeToElement(new { text = "hello" }),
            cancellationToken);

        var tool = Assert.Single(tools);
        Assert.Equal("echo", tool.Name);
        Assert.True(tool.IsReadOnly);
        Assert.False(result.IsError);
        Assert.Equal("echo:hello", result.Content);
        Assert.True(server.SessionHeaderObserved);
        Assert.True(server.CustomHeaderObserved);
        await client.DisposeAsync();
        Assert.True(server.DeleteObserved);
    }
}
