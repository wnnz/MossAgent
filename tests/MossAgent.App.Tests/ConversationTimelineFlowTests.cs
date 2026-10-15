using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealSettingsModelAndToolFlow_PreservesElapsedTimeAcrossReopenWithoutReplay(bool approved)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("timeline fixture", transport: McpTransportKind.StreamableHttp);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        using var timeline = new ConversationTimelineViewModel();
        timeline.Bind(fixture.Workspace.Messages, true);
        var turn = Assert.Single(timeline.Turns);
        Assert.True(turn.IsExpanded);
        Assert.Null(turn.Elapsed);
        if (approved) fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(approved ? null : "approval_denied", approved);
        timeline.Bind(fixture.Workspace.Messages, false);
        Assert.Same(turn, Assert.Single(timeline.Turns));
        Assert.NotNull(turn.UserMessage!.CreatedAt);
        Assert.NotNull(turn.Response!.CreatedAt);
        Assert.NotNull(turn.Elapsed);
        Assert.Equal(turn.Response.CreatedAt - turn.UserMessage.CreatedAt, turn.Elapsed);
        var elapsed = turn.Elapsed;
        var timestamp = turn.Timestamp;
        var start = turn.UserMessage.CreatedAt;
        var end = turn.Response.CreatedAt;
        var requests = fixture.Model.Requests.Count;
        await fixture.ReopenAsync();
        timeline.Bind(fixture.Workspace.Messages, false);
        var restored = Assert.Single(timeline.Turns);
        Assert.Equal(start, restored.UserMessage!.CreatedAt);
        Assert.Equal(end, restored.Response!.CreatedAt);
        Assert.Equal(elapsed, restored.Elapsed);
        Assert.Equal(timestamp, restored.Timestamp);
        Assert.False(restored.IsExpanded);
        Assert.Equal(requests, fixture.Model.Requests.Count);
        Assert.Equal(approved, fixture.HttpServer.CallStarted.Task.IsCompleted);
    }
}
