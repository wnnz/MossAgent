using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceHttpMcpFlowTests
{
    [Fact]
    public async Task HttpSettingsToApprovedSseCall_RestoresConversationAndAudit()
    {
        var fixture = await WorkspaceMcpFixture.CreateAsync("真实 HTTP 回复 🙂",
            transport: McpTransportKind.StreamableHttp);
        var server = fixture.HttpServer;
        try
        {
            fixture.Start();
            await fixture.WaitForApprovalAsync();
            Assert.Equal(fixture.ToolName, fixture.Approval.ToolName);
            Assert.False(server.CallStarted.Task.IsCompleted);
            fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
            await fixture.WaitForCompletionAsync();
            Assert.Equal("真实 HTTP 回复 🙂", (await fixture.AssertPersistedAsync(null, true)).Content);
            Assert.True(server.CustomHeaderObserved);
            Assert.True(server.SessionHeaderObserved);
            Assert.Equal(2, fixture.Model.Requests.Count);
        }
        finally { await fixture.DisposeAsync(); }
        Assert.True(server.DeleteObserved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingHttpCall_DenialOrStopNeverSendsToolRequest(bool stop)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run",
            transport: McpTransportKind.StreamableHttp);
        fixture.Start();
        await fixture.WaitForApprovalAsync();

        if (stop) fixture.Workspace.PrimaryActionCommand.Execute(null);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        await fixture.AssertPersistedAsync(stop ? "tool_cancelled" : "approval_denied", stop ? null : false,
            stop ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed);
        Assert.Equal(stop ? 1 : 2, fixture.Model.Requests.Count);
    }

    [Fact]
    public async Task HttpToolWithoutReadOnlyHint_IsRejectedByReadOnlyPolicy()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run",
            transport: McpTransportKind.StreamableHttp);
        fixture.Start(ApprovalPolicy.ReadOnly);
        await fixture.WaitForCompletionAsync();

        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        await fixture.AssertPersistedAsync("capability_denied", false);
    }

    [Fact]
    public async Task StoppingRunningHttpCall_NotifiesServerAndPersistsCancellation()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("wait", transport: McpTransportKind.StreamableHttp);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        var requestId = await fixture.HttpServer.CallStarted.Task.WaitAsync(fixture.Token);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(requestId, await fixture.HttpServer.CancellationReceived.Task.WaitAsync(fixture.Token));
        await fixture.AssertPersistedAsync("tool_cancelled", true, AgentTaskStatus.Cancelled);
        Assert.Single(fixture.Model.Requests);
    }

    [Theory]
    [InlineData("verbose", null)]
    [InlineData("error", "tool_failed")]
    [InlineData("tool-error", "mcp_tool_error")]
    public async Task HttpOutputs_AreBoundedAndRemoteErrorsNeverReachHistoryOrModel(string message, string? error)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync(message, transport: McpTransportKind.StreamableHttp);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        var result = await fixture.AssertPersistedAsync(error, true);
        Assert.InRange(result.OutputCharacterCount, 1, 64 * 1024);
        Assert.DoesNotContain("fixture-secret-marker", result.Summary + result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-secret-marker", fixture.Model.Requests[1], StringComparison.Ordinal);
        if (error is null) Assert.Contains("[截断]", result.Content);
    }
}
