using System.Text.Json;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioProcessOwnershipTests
{
    [Fact]
    public async Task ParentExit_ReclaimsDescendantsWithoutWaitingForClientDisposal()
    {
        await using var fixture = new McpStdioProcessFixture();
        await fixture.Client.InitializeAsync(fixture.Token);
        var processes = await fixture.BindProcessesAsync();
        Assert.All(processes, process => Assert.False(process.HasExited));

        await Assert.ThrowsAsync<IOException>(() => fixture.CallAsync("exit"));

        await fixture.AssertExitedAsync(processes);
    }

    [Fact]
    public async Task Dispose_ReclaimsServerAndDescendantsAndFailsPendingCalls()
    {
        await using var fixture = new McpStdioProcessFixture();
        await fixture.Client.InitializeAsync(fixture.Token);
        var processes = await fixture.BindProcessesAsync();
        var call = fixture.CallAsync("wait");
        await fixture.WaitForFileAsync("waiting.id");

        await fixture.Client.DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => call);
        await fixture.AssertExitedAsync(processes);
    }

    [Fact]
    public async Task CancelledInitialization_ReclaimsServerAndDescendants()
    {
        await using var fixture = new McpStdioProcessFixture("hang-initialize");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var initialization = fixture.Client.InitializeAsync(cancellation.Token);
        var processes = await fixture.BindProcessesAsync();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        await fixture.AssertExitedAsync(processes);
    }

    [Fact]
    public async Task StartedServer_ReceivesSavedArgumentsEnvironmentAndWorkingDirectory()
    {
        await using var fixture = new McpStdioProcessFixture(child: false);
        await fixture.Client.InitializeAsync(fixture.Token);

        using var startup = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.Root, "startup.json"), fixture.Token));

        Assert.Equal("参数 with spaces", startup.RootElement.GetProperty("argument").GetString());
        Assert.Equal(fixture.Root, startup.RootElement.GetProperty("directory").GetString());
        Assert.Equal("环境 🚀", startup.RootElement.GetProperty("environment").GetString());
    }
}
