using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsMutationProcessTests
{
    [Fact]
    public async Task Dispose_DuringSaveReconnectReleasesOwnedServerAndChildBeforeContainerShutdown()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not send");
        var model = fixture.McpSettings;
        model.OpenEditFormCommand.Execute(Assert.Single(model.Items));
        var serverMarker = Path.Combine(fixture.ServerRoot, "server.pid");
        var childMarker = Path.Combine(fixture.ServerRoot, "child.pid");
        File.Delete(serverMarker);
        await File.WriteAllTextAsync(Path.Combine(fixture.ServerRoot, "child.ps1"), """
            [IO.File]::WriteAllText('child.pid.tmp', [string]$PID)
            [IO.File]::Move('child.pid.tmp', 'child.pid', $true)
            Start-Sleep -Seconds 30
            [IO.File]::WriteAllText('late-child.txt', 'must not survive')
            """, fixture.Token);
        model.EnvironmentJson = JsonSerializer.Serialize(new
        {
            MOSSAGENT_MCP_FIXTURE_MODE = "hang-initialize",
            MOSSAGENT_MCP_FIXTURE_CHILD = "1"
        });
        var run = model.SaveCommand.ExecuteAsync(null);
        await fixture.WaitForFileAsync("server.pid");
        await fixture.WaitForFileAsync("child.pid");
        using var server = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(serverMarker, fixture.Token)));
        using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(childMarker, fixture.Token)));
        Assert.False(run.IsCompleted);
        Assert.False(server.HasExited);
        Assert.False(child.HasExited);
        Assert.Empty(fixture.Model.Requests);

        model.Dispose();
        var notifications = new List<string?>();
        model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
        await run.WaitAsync(fixture.Token);
        await server.WaitForExitAsync(fixture.Token);
        await child.WaitForExitAsync(fixture.Token);

        Assert.Empty(notifications);
        Assert.False(File.Exists(Path.Combine(fixture.ServerRoot, "late-child.txt")));
        Assert.DoesNotContain(fixture.Tools.Descriptors, tool => tool.Name == fixture.ToolName);
        Assert.Empty(fixture.Model.Requests);
        var persisted = Assert.Single(await fixture.McpConfigurations.GetAllAsync(fixture.Token));
        Assert.Equal(model.SelectedItem?.Id, persisted.Id);
        Assert.Equal(model.EnvironmentJson, persisted.EnvironmentJson);
        Assert.True(persisted.IsEnabled);
        Assert.False(model.CanEdit);
    }
}
