using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpReconnectWorkflowTests
{
    [Fact]
    public async Task Reconnect_RecoversPersistedEndpointAndExecutesThroughApprovalAndAudit()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("重连后真实 MCP 回复 🙂",
            transport: McpTransportKind.StreamableHttp);
        var original = Assert.Single(fixture.McpSettings.Items);
        var unavailable = original with { Url = new Uri(fixture.HttpServer.Endpoint, "../unavailable/") };
        var unavailableSaved = await fixture.McpConfigurations.SaveAsync(unavailable, fixture.Token);

        await fixture.McpSettings.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(unavailableSaved, Assert.Single(fixture.McpSettings.Items));
        Assert.Contains(original.Name, fixture.McpManager.Errors.Keys);
        Assert.DoesNotContain(fixture.Tools.Descriptors, tool => tool.Name == fixture.ToolName);
        Assert.Empty(fixture.Model.Requests);
        Assert.False(fixture.Approval.IsPending);
        var restored = await fixture.McpConfigurations.SaveAsync(
            original with { Revision = unavailableSaved.Revision }, fixture.Token);

        await fixture.McpSettings.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(restored, Assert.Single(fixture.McpSettings.Items));
        Assert.Empty(fixture.McpManager.Errors);
        Assert.Contains(fixture.Tools.Descriptors, tool => tool.Name == fixture.ToolName);
        Assert.Empty(fixture.Model.Requests);
        Assert.False(fixture.McpSettings.IsFormOpen);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Equal(fixture.ToolName, fixture.Approval.ToolName);
        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.Equal("重连后真实 MCP 回复 🙂", (await fixture.AssertPersistedAsync(null, true)).Content);
        Assert.True(fixture.HttpServer.CustomHeaderObserved);
        Assert.True(fixture.HttpServer.SessionHeaderObserved);
        Assert.Equal(2, fixture.Model.Requests.Count);
        Assert.Equal(restored, Assert.Single(await fixture.McpConfigurations.GetAllAsync(fixture.Token)));
    }
}
