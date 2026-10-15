using System.Text.Json;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioProtocolSafetyTests
{
    [Fact]
    public async Task ConcurrentCalls_PreserveCompleteUnicodeFrames()
    {
        await using var fixture = new McpStdioProcessFixture(child: false);
        await fixture.Client.InitializeAsync(fixture.Token);
        var messages = Enumerable.Range(0, 24).Select(index => $"{index}:" + new string('测', 4096)).ToArray();

        var results = await Task.WhenAll(messages.Select(message => fixture.CallAsync(message)));

        Assert.Equal(messages, results.Select(result => result.Content));
    }

    [Fact]
    public async Task Cancellation_NotifiesServerAndIgnoresLateReplyWithoutClosingOtherCalls()
    {
        await using var fixture = new McpStdioProcessFixture(child: false);
        await fixture.Client.InitializeAsync(fixture.Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var call = fixture.CallAsync("wait", cancellation.Token);
        await fixture.WaitForFileAsync("waiting.id");

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await fixture.WaitForFileAsync("cancelled.json");
        using var notification = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.Root, "cancelled.json"), fixture.Token));
        var requestId = long.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "waiting.id"), fixture.Token));
        Assert.Equal(requestId, notification.RootElement.GetProperty("requestId").GetInt64());
        Assert.Equal("仍可调用", (await fixture.CallAsync("仍可调用")).Content);
    }

    [Fact]
    public async Task OversizedUnterminatedOutput_ClosesAndReclaimsServer()
    {
        await using var fixture = new McpStdioProcessFixture("oversized-stdout", child: false);

        await Assert.ThrowsAsync<IOException>(() => fixture.Client.InitializeAsync(fixture.Token));
        await Assert.ThrowsAsync<IOException>(() => fixture.CallAsync("after failure"));
    }

    [Fact]
    public async Task LargeErrorOutput_DrainsWithoutBlockingOrEnteringToolResult()
    {
        await using var fixture = new McpStdioProcessFixture(child: false);
        await fixture.Client.InitializeAsync(fixture.Token);

        var result = await fixture.CallAsync("large-stderr");

        Assert.Equal("large-stderr", result.Content);
    }

    [Fact]
    public async Task ProtocolError_DoesNotEchoServerSecretsAndKeepsConnectionUsable()
    {
        await using var fixture = new McpStdioProcessFixture(child: false);
        await fixture.Client.InitializeAsync(fixture.Token);

        var error = await Assert.ThrowsAsync<IOException>(() => fixture.CallAsync("error"));

        Assert.DoesNotContain("fixture-secret-marker", error.ToString());
        Assert.Equal("hello", (await fixture.CallAsync("hello")).Content);
    }
}
