using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Platform.Windows.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioManagerDeadlineTests
{
    [Fact]
    public async Task ProductionDefaultDeadlineRemainsFifteenSecondsAndReclaimsDescendants()
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync("hang-initialize");
        var connections = new SqliteConnectionFactory(new AppDataPaths(Path.Combine(fixture.Server.Root, "data")));
        await using var manager = new McpManager(fixture.Configurations,
            new McpHttpClientFactory(new SqliteConfigurationRepository(connections)), fixture.Tools,
            new WindowsTaskProcessLauncher(), timeProvider: fixture.Clock);
        var reload = manager.ReloadAsync(fixture.Server.Token);
        var processes = await fixture.Server.BindProcessesAsync();

        Assert.Equal(TimeSpan.FromSeconds(15), fixture.Clock.LastDueTime);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(14999));
        Assert.False(reload.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await reload;

        Assert.Empty(fixture.Tools.Descriptors);
        Assert.Contains("超时", Assert.Single(manager.Errors).Value);
        await fixture.Server.AssertExitedAsync(processes);
    }
}
