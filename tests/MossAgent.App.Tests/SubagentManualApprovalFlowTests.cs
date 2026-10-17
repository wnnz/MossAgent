using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SubagentManualApprovalFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, false, true)]
    [InlineData(ProviderProtocol.OpenAiResponses, true, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true, true)]
    public Task RenderedApproval_ExecutesSavedChildAndRestoresVisibleDialogueWithoutReplay(
        ProviderProtocol protocol, bool shareContext, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(protocol, shareContext);
        var profile = Assert.Single(await fixture.Subagents.GetAllAsync(fixture.Token));
        var window = SubagentApprovalInteraction.Show(fixture.Main, dark);
        try
        {
            fixture.Start(ApprovalPolicy.AskEveryTime);
            await SubagentApprovalInteraction.WaitUntilAsync(() => fixture.Main.Approval.IsPending, fixture.Token);
            if (dark) fixture.Main.SelectSettingsTabCommand.Execute("5");
            fixture.Workspace.ComposerText = "未发送的下一条草稿。";
            SubagentApprovalInteraction.Flush(window);
            AssertPending(fixture);
            SubagentApprovalInteraction.Save(window, $"pending-{protocol}-{shareContext}-{dark}");
            SubagentApprovalInteraction.Decide(window, fixture.Main.Approval, approve: true, keyboard: dark);
            await SubagentApprovalInteraction.WaitUntilAsync(() => !fixture.Workspace.IsRunning, fixture.Token);

            Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
            Assert.Equal(dark, fixture.Main.IsSettingsOpen);
            Assert.Equal("未发送的下一条草稿。", fixture.Workspace.ComposerText);
            fixture.Main.CloseSettingsCommand.Execute(null);
            await AssertCompletedAsync(fixture, shareContext);
            var row = Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool);
            SubagentApprovalInteraction.AssertVisibleDialogue(window, row, dark);
            SubagentApprovalInteraction.Save(window, $"completed-{protocol}-{shareContext}-{dark}");
            var taskId = fixture.Workspace.Catalog.SelectedTask.Id;
            window.DataContext = null;
            await fixture.ReopenAsync(taskId);
            window.DataContext = fixture.Main;
            SubagentApprovalInteraction.Flush(window);
            SubagentApprovalInteraction.AssertVisibleDialogue(window,
                Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool), dark);
            Assert.False(fixture.Main.Approval.IsPending);
            Assert.Equal(0, fixture.Main.Approval.PendingCount);
            Assert.Equal(2, fixture.ParentServer.Requests.Count);
            Assert.Equal(2, fixture.ChildServer.Requests.Count);
            Assert.Equal(profile, Assert.Single(await fixture.Subagents.GetAllAsync(fixture.Token)));
            Assert.Equal(ApprovalPolicy.AskEveryTime, fixture.Workspace.Catalog.SelectedTask!.ApprovalPolicy);
            SubagentApprovalInteraction.Save(window, $"restored-{protocol}-{shareContext}-{dark}");
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    internal static void AssertPending(SubagentProtocolFixture fixture)
    {
        Assert.Equal("subagent.run", fixture.Main.Approval.ToolName);
        Assert.Equal(1, fixture.Main.Approval.PendingCount);
        Assert.NotNull(fixture.Main.Approval.CurrentRequestId);
        Assert.Equal(fixture.ProjectRoot, fixture.Main.Approval.WorkingDirectory);
        Assert.Single(fixture.ParentServer.Requests);
        Assert.Empty(fixture.ChildServer.Requests);
        Assert.True(fixture.Workspace.IsRunning);
        Assert.True(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
        Assert.True(Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool).IsToolRunning);
    }

    private static async Task AssertCompletedAsync(SubagentProtocolFixture fixture, bool shareContext)
    {
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Equal(2, fixture.ChildServer.Requests.Count);
        SubagentProtocolFlowTests.AssertChildRequest(fixture, shareContext);
        Assert.Contains(SubagentProtocolResponses.Evidence, fixture.ChildServer.Requests[1].Body.GetRawText());
        Assert.DoesNotContain(SubagentProtocolResponses.PrivateEvidence, fixture.ChildServer.Requests[1].Body.GetRawText());
        var audits = await fixture.Audits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask!.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit =>
        {
            Assert.True(audit.IsSuccess);
            Assert.True(audit.ApprovalGranted);
            Assert.NotNull(audit.CompletedAt);
            Assert.Equal(fixture.Workspace.Catalog.SelectedTask.Id, audit.TaskId);
        });
        Assert.Single(audits, audit => audit.ToolName == "subagent.run" && audit.CallId == "parent-delegate");
        var childAudit = Assert.Single(audits, audit => audit.ToolName == "filesystem.read_file");
        Assert.StartsWith("parent-delegate:", childAudit.CallId);
        if (fixture.Protocol == ProviderProtocol.OpenAiResponses) Assert.Equal("parent-delegate:child-read", childAudit.CallId);
        else Assert.True(Guid.TryParseExact(childAudit.CallId["parent-delegate:".Length..], "N", out _));
        Assert.False(fixture.Main.Approval.IsPending);
        Assert.False(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
        Assert.Equal(SubagentProtocolResponses.Evidence, await File.ReadAllTextAsync(fixture.AllowedFile, fixture.Token));
        Assert.Equal(SubagentProtocolResponses.PrivateEvidence, await File.ReadAllTextAsync(fixture.OutsideFile, fixture.Token));
        var saved = await fixture.Repository.GetMessagesAsync(fixture.Workspace.Catalog.SelectedTask.Id, fixture.Token);
        var restored = WorkspaceConversationMapper.ToChatMessage(Assert.Single(saved, message => message.ToolCallId == "parent-delegate"));
        Assert.True(restored.Tool!.IsSuccess);
        Assert.Contains(restored.Tool.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
    }
}
