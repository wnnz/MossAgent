using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppStartupReadinessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialToolDiscovery_PreventsPrematureRunsButKeepsTheComposerAndSettingsUsable(bool hasWarning)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run",
            transport: McpTransportKind.StreamableHttp);
        var workspace = fixture.Workspace;
        var startup = workspace.Startup;
        var changes = 0;
        workspace.PrimaryActionCommand.CanExecuteChanged += (_, _) => changes++;
        workspace.ComposerText = "保留输入，不要提前发送";
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));

        startup.Begin();
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);

        Assert.Equal("保留输入，不要提前发送", workspace.ComposerText);
        Assert.Empty(fixture.Model.Requests);
        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        Assert.True(fixture.McpSettings.OpenCreateFormCommand.CanExecute(null));
        startup.Complete(hasWarning);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.True(changes >= 3);
        if (hasWarning) Assert.Equal(startup.Status, fixture.McpSettings.Status);
        else Assert.Contains("1 个工具", fixture.McpSettings.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosedViewModels_UnsubscribeFromStartupNotifications()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run",
            transport: McpTransportKind.StreamableHttp);
        var workspace = fixture.Workspace;
        var changes = 0;
        workspace.PrimaryActionCommand.CanExecuteChanged += (_, _) => changes++;
        workspace.Startup.Begin();
        var previousStatus = fixture.McpSettings.Status;
        workspace.Dispose();
        fixture.McpSettings.Dispose();
        var previousChanges = changes;

        workspace.Startup.Complete(true);

        Assert.Equal(previousChanges, changes);
        Assert.Equal(previousStatus, fixture.McpSettings.Status);
    }
}
