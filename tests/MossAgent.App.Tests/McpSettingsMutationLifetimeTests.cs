using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsMutationLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_DuringPreflightReadPreventsNewWriteAndReconnect(bool toggle)
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        fixture.Model.Name = "captured-before-close";
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = toggle ? fixture.Model.ToggleEnabledCommand.ExecuteAsync(original)
            : fixture.Model.SaveCommand.ExecuteAsync(null);
        fixture.Model.Dispose();

        pending.SetResult([original]);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.True(fixture.Repository.ReadToken.IsCancellationRequested);
        Assert.Same(original, Assert.Single(fixture.Model.Items));
    }

    [Fact]
    public async Task Dispose_AfterSaveStartsDoesNotReconnectOrPublishLateCompletion()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        fixture.Model.Name = "committed-during-close";
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeSave = _ => pending.Task;
        var run = fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, fixture.Repository.WriteCalls);
        fixture.Model.Dispose();
        var notifications = new List<string?>();
        fixture.Model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);

        pending.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("committed-during-close", Assert.Single(fixture.Repository.Items).Name);
        Assert.True(fixture.Repository.WriteToken.IsCancellationRequested);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.Empty(notifications);
        Assert.Same(original, Assert.Single(fixture.Model.Items));
    }

    [Fact]
    public async Task Dispose_AfterConfirmedDeleteStartsDoesNotResetEditorOrReconnect()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.BeforeDelete = _ => pending.Task;
        var run = fixture.Model.DeleteCommand.ExecuteAsync(null);
        fixture.Model.Dispose();
        var notifications = new List<string?>();
        fixture.Model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);

        pending.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Repository.Items);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.True(fixture.Repository.WriteToken.IsCancellationRequested);
        Assert.Same(original, fixture.Model.SelectedItem);
        Assert.Equal(original.Name, fixture.Model.Name);
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task DisposedCommands_DoNotReadWriteOrReconnect()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        var reads = fixture.Repository.ReadCalls;
        fixture.Model.Dispose();

        await fixture.Model.SaveCommand.ExecuteAsync(null);
        await fixture.Model.ToggleEnabledCommand.ExecuteAsync(original);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        await fixture.Model.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(reads, fixture.Repository.ReadCalls);
        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Equal(0, fixture.Manager.ReloadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_DuringPreflightReadPreventsNewWriteAndAllowsRetry(bool toggle)
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var command = toggle ? fixture.Model.ToggleEnabledCommand : fixture.Model.SaveCommand;
        var run = command.ExecuteAsync(toggle ? original : null);

        command.Cancel();
        pending.SetResult([original]);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(fixture.Repository.ReadToken.IsCancellationRequested);
        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.Contains("已取消", fixture.Model.Status);
        Assert.True(fixture.Model.CanEdit);
        Assert.True(fixture.Model.IsFormOpen);
        fixture.Repository.Reader = null;
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, fixture.Repository.WriteCalls);
    }
}
