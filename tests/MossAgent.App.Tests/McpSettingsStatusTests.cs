using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsStatusTests
{
    [Fact]
    public async Task InitialReadFailureAndSuccessfulRetryPublishDistinctSeverity()
    {
        using var fixture = new McpSettingsReloadFixture(initialFailure: true);
        Assert.True(fixture.Model.HasStatusError);
        fixture.Repository.Reader = null;
        await fixture.Model.ReloadCommand.ExecuteAsync(null);
        Assert.False(fixture.Model.HasStatusError);
    }

    [Fact]
    public async Task ValidationFailureClearsWhenOpeningANewDraft()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Model.OpenCreateFormCommand.Execute(null);
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.HasStatusError);
        Assert.True(fixture.Model.IsFormOpen);
        fixture.Model.OpenCreateFormCommand.Execute(null);
        Assert.False(fixture.Model.HasStatusError);
        Assert.Empty(fixture.Model.Status);
    }

    [Fact]
    public async Task SavedButDisconnectedRemainsErrorUntilConnectionRecovers()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Model.OpenCreateFormCommand.Execute(null);
        fixture.Model.Name = "recovery";
        fixture.Model.Command = "not-started";
        fixture.Manager.Errors = new Dictionary<string, string> { ["recovery"] = "连接失败" };
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.IsFormOpen);
        Assert.True(fixture.Model.HasStatusError);
        fixture.Manager.Errors = new Dictionary<string, string>();
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.False(fixture.Model.IsFormOpen);
        Assert.False(fixture.Model.HasStatusError);
    }

    [Fact]
    public async Task DisablingAnUnreachableServerClearsItsConnectionError()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Manager.Errors = new Dictionary<string, string> { ["initial"] = "连接失败" };
        await fixture.Model.ToggleEnabledCommand.ExecuteAsync(Assert.Single(fixture.Model.Items));
        Assert.True(fixture.Model.HasStatusError);
        await fixture.Model.ToggleEnabledCommand.ExecuteAsync(Assert.Single(fixture.Model.Items));
        Assert.False(fixture.Model.HasStatusError);
    }

    [Fact]
    public async Task CancelIsNeutralEvenAfterAnEarlierError()
    {
        using var fixture = new McpSettingsReloadFixture(blockReconnect: true);
        fixture.Repository.Reader = _ => Task.FromException<IReadOnlyList<MossAgent.Domain.McpServerProfile>>(
            new IOException("synthetic-private-error"));
        fixture.Model.OpenCreateFormCommand.Execute(null);
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.HasStatusError);
        fixture.Model.CloseFormCommand.Execute(null);
        var run = fixture.Model.ReloadCommand.ExecuteAsync(null);
        await fixture.Manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(fixture.Model.HasStatusError);
        fixture.Model.CancelReloadCommand.Execute(null);
        await run.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(fixture.Model.HasStatusError);
        Assert.Contains("已取消", fixture.Model.Status);
    }
}
