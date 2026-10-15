using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceHistoryFlowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SettingsThroughActualFileToolAndSqliteRestore_ContinueWithoutReplayingOrBypassingReadOnly(bool restart, bool mutation)
    {
        await using var fixture = await WorkspaceHistoryFlowFixture.CreateAsync(mutation);
        await fixture.SendAsync("Read evidence.txt once.");
        var taskId = fixture.Workspace.Catalog.SelectedTask!.Id;
        Assert.Contains(fixture.Workspace.Messages, message => message.Content == HistoryContinuationModelServer.FirstReply);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.IsAssistantMessage
            && message.Content.Contains(HistoryContinuationModelServer.Evidence, StringComparison.Ordinal));
        if (restart) await fixture.ReopenAsync(taskId);

        await fixture.SendAsync("Use the previously read evidence without reading it again.");
        await fixture.Server.Completion.WaitAsync(fixture.Token);

        Assert.Equal(taskId, fixture.Workspace.Catalog.SelectedTask!.Id);
        Assert.True(fixture.Server.SawHistoricalEvidence);
        Assert.True(fixture.Server.FollowupHadNoNativeReplay);
        Assert.Equal(mutation ? 4 : 3, fixture.Server.Requests.Count);
        Assert.Equal(HistoryContinuationModelServer.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var audits = await fixture.Audits.GetRecentAsync(taskId, 10, fixture.Token);
        Assert.True(Assert.Single(audits, audit => audit.CallId == "history-read").IsSuccess);
        Assert.Equal(mutation ? 2 : 1, audits.Count);
        if (mutation) Assert.Equal("capability_denied", Assert.Single(audits, audit => audit.CallId == "history-write").ErrorCode);
        var persisted = await fixture.Repository.GetMessagesAsync(taskId, fixture.Token);
        Assert.Equal(2, persisted.Count(message => message.Role == MessageRole.User));
        Assert.Equal(mutation ? 2 : 1, persisted.Count(message => message.Role == MessageRole.Tool));
        var read = WorkspaceConversationMapper.ToChatMessage(Assert.Single(persisted, message => message.ToolCallId == "history-read"));
        Assert.Contains(HistoryContinuationModelServer.Evidence, read.Tool!.Details);
        Assert.Contains(fixture.Workspace.Messages, message => message.Content == HistoryContinuationModelServer.ContinuedReply);
        Assert.False(fixture.Approval.IsPending);
        Assert.Equal(0, fixture.Approval.PendingCount);
    }
}
