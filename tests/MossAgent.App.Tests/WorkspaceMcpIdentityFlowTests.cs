using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceMcpIdentityFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CollidingSavedServers_KeepModelCallsApprovalAndAuditBoundToTheOriginalProfile(bool approved)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("profile-specific reply",
            transport: McpTransportKind.StreamableHttp);
        await using var other = new WorkspaceMcpHttpServer();
        var originalToolName = fixture.ToolName;
        var settings = fixture.McpSettings;
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "workflow!";
        settings.Transport = McpTransportKind.StreamableHttp;
        settings.Url = other.Endpoint.AbsoluteUri;
        settings.IsEnabled = true;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen, settings.Status);
        Assert.Equal(2, settings.Items.Count);

        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Equal(originalToolName, fixture.Approval.ToolName);
        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        Assert.False(other.CallStarted.Task.IsCompleted);
        if (approved) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        var result = await fixture.AssertPersistedAsync(approved ? null : "approval_denied", approved);
        Assert.Equal(approved, fixture.HttpServer.CallStarted.Task.IsCompleted);
        Assert.False(other.CallStarted.Task.IsCompleted);
        if (approved) Assert.Equal("profile-specific reply", result.Content);
        Assert.Equal(2, fixture.Model.Requests.Count);
        await fixture.ReopenAsync();
        await fixture.AssertPersistedAsync(approved ? null : "approval_denied", approved);
        Assert.False(other.CallStarted.Task.IsCompleted);
    }
}
