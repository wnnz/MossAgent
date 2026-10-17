using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SubagentManualApprovalBoundaryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, false, true)]
    [InlineData(ProviderProtocol.OpenAiResponses, true, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true, true)]
    public Task DenialAndCancellation_DoNotStartTheChildOrReplayAStaleApproval(
        ProviderProtocol protocol, bool dark, bool cancel) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(protocol, shareContext: true);
        var window = SubagentApprovalInteraction.Show(fixture.Main, dark);
        try
        {
            fixture.Start(ApprovalPolicy.AskEveryTime);
            await SubagentApprovalInteraction.WaitUntilAsync(() => fixture.Main.Approval.IsPending, fixture.Token);
            SubagentManualApprovalFlowTests.AssertPending(fixture);
            var requestId = fixture.Main.Approval.CurrentRequestId;
            fixture.Main.Approval.ApproveCommand.Execute(Guid.NewGuid());
            Assert.Equal(requestId, fixture.Main.Approval.CurrentRequestId);
            Assert.Empty(fixture.ChildServer.Requests);
            if (cancel) fixture.Workspace.PrimaryActionCommand.Execute(null);
            else SubagentApprovalInteraction.Decide(window, fixture.Main.Approval, approve: false, keyboard: dark);
            await SubagentApprovalInteraction.WaitUntilAsync(() => !fixture.Workspace.IsRunning, fixture.Token);
            fixture.Main.Approval.ApproveCommand.Execute(requestId);
            SubagentApprovalInteraction.Flush(window);
            Assert.False(fixture.Main.Approval.ApproveCommand.CanExecute(requestId));
            Assert.False(fixture.Main.Approval.IsPending);
            Assert.False(SubagentApprovalInteraction.View(window).IsEffectivelyVisible);
            Assert.False(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
            Assert.Empty(fixture.ChildServer.Requests);
            Assert.Equal(cancel ? 1 : 2, fixture.ParentServer.Requests.Count);
            Assert.Equal(cancel ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
            var audit = Assert.Single(await fixture.Audits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask.Id, 10, fixture.Token));
            Assert.False(audit.IsSuccess);
            Assert.Equal("subagent.run", audit.ToolName);
            Assert.Equal(cancel ? "tool_cancelled" : "approval_denied", audit.ErrorCode);
            Assert.NotNull(audit.CompletedAt);
            if (!cancel) Assert.False(audit.ApprovalGranted);
            var taskId = fixture.Workspace.Catalog.SelectedTask.Id;
            window.DataContext = null;
            await fixture.ReopenAsync(taskId);
            window.DataContext = fixture.Main;
            SubagentApprovalInteraction.Flush(window);
            Assert.False(fixture.Main.Approval.IsPending);
            Assert.Empty(fixture.ChildServer.Requests);
            Assert.Equal(cancel ? 1 : 2, fixture.ParentServer.Requests.Count);
            Assert.DoesNotContain(fixture.Workspace.Messages, message => message.Content == SubagentProtocolResponses.ChildReply);
            Assert.Equal(SubagentProtocolResponses.PrivateEvidence, await File.ReadAllTextAsync(fixture.OutsideFile, fixture.Token));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
