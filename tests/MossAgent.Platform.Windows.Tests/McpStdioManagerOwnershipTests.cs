using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioManagerOwnershipTests
{
    [Theory]
    [InlineData("disable")]
    [InlineData("delete")]
    [InlineData("reload")]
    public async Task ConfigurationChange_ReclaimsPreviousServerAndDescendants(string change)
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync();
        var token = fixture.Server.Token;
        await fixture.Manager.ReloadAsync(token);
        var processes = await fixture.Server.BindProcessesAsync();
        Assert.Empty(fixture.Manager.Errors);
        Assert.All(processes, process => Assert.False(process.HasExited));
        Assert.Single(fixture.Tools.Descriptors);
        if (change == "disable")
        {
            var persisted = Assert.Single(await fixture.Configurations.GetAllAsync(token));
            await fixture.Configurations.SaveAsync(persisted with { IsEnabled = false }, token);
        }
        else if (change == "delete")
            await fixture.Configurations.DeleteAsync(fixture.Server.Profile.Id, token);

        await fixture.Manager.ReloadAsync(token);

        await fixture.Server.AssertExitedAsync(processes);
        Assert.Empty(fixture.Manager.Errors);
        if (change == "reload") Assert.Single(fixture.Tools.Descriptors);
        else Assert.Empty(fixture.Tools.Descriptors);
    }

    [Fact]
    public async Task InitializationTimeout_ReclaimsDescendantsWithoutRegisteringTools()
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync("hang-initialize");
        var reload = fixture.Manager.ReloadAsync(fixture.Server.Token);
        var processes = await fixture.Server.BindProcessesAsync();

        Assert.Equal(TimeSpan.FromSeconds(3), fixture.Clock.LastDueTime);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(2999));
        Assert.False(reload.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await reload;

        Assert.Empty(fixture.Tools.Descriptors);
        Assert.Contains("超时", Assert.Single(fixture.Manager.Errors).Value);
        await fixture.Server.AssertExitedAsync(processes);
    }

    [Fact]
    public async Task CallerCancellation_IsPreservedAndReclaimsDescendants()
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync("hang-initialize");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Server.Token);
        var reload = fixture.Manager.ReloadAsync(cancellation.Token);
        var processes = await fixture.Server.BindProcessesAsync();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reload);
        Assert.Empty(fixture.Tools.Descriptors);
        Assert.Empty(fixture.Manager.Errors);
        await fixture.Server.AssertExitedAsync(processes);
    }
}
