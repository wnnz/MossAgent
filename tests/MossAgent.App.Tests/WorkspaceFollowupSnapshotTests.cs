using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedAttachmentsUseOwnedSnapshotsAndRejectTamperedBytes(bool tamper)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var sourcePath = Path.Combine(fixture.DataRoot, "selected-input.txt");
        const string text = "queued-immutable-attachment";
        var attachment = new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(sourcePath, "selected-input.txt", text,
            Encoding.UTF8.GetByteCount(text)), _ => { }, authorizationStamp: WorkspaceAttachmentAuthorization.CreateSelection(sourcePath));
        workspace.Attachments.Items.Add(attachment);
        workspace.ComposerText = "使用已保存的附件";
        Assert.True(workspace.Queue.EnqueueCommand.CanExecute(null));
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        var row = Assert.Single(workspace.Queue.Items);
        Assert.Single(row.Item.Attachments);
        Assert.Empty(workspace.Attachments.Items);
        if (tamper)
        {
            var metadata = row.Item.Attachments[0];
            var path = Assert.Single(Directory.EnumerateFiles(Path.Combine(fixture.DataRoot, "artifacts"), metadata.Id.ToString("N") + ".txt", SearchOption.AllDirectories));
            await File.WriteAllTextAsync(path, "modified", fixture.Token);
        }
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && !workspace.Queue.IsBusy
            && (workspace.Queue.HasNotice || fixture.Server.Requests.Count > 1));
        if (tamper)
        {
            Assert.Single(fixture.Server.Requests);
            Assert.Single(workspace.Queue.Items);
            Assert.Contains("附件无法校验", workspace.Queue.Notice);
        }
        else
        {
            Assert.Equal(2, fixture.Server.Requests.Count);
            Assert.Contains(text, fixture.Server.Requests[1].GetRawText());
            Assert.Empty(workspace.Queue.Items);
            var saved = await fixture.Tasks.GetMessagesAsync(workspace.Catalog.SelectedTask!.Id, fixture.Token);
            Assert.Single(Assert.Single(saved, message => message.Id == row.Id).Attachments!);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedAgentPromptAndModelAreUsedButDisabledAgentsCannotBeDispatched(bool disable)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts,
            followups: fixture.FollowupServices(fixture.Followups), configureRunController: controller =>
                controller.RunFinished += (_, _) => { entered.TrySetResult(); return release.Task.WaitAsync(fixture.Token); });
        await workspace.LoadAsync();
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "initial";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var agent = new AgentProfile(Guid.NewGuid(), "queue agent", "saved-agent-instructions", fixture.AlternativeModel.ProviderId,
            fixture.AlternativeModel.Id, "high", true);
        await fixture.AgentProfiles.SaveAsync(agent, fixture.Token);
        await workspace.ReloadAgentsAsync();
        workspace.SelectedAgent = agent;
        workspace.ComposerText = "queue-agent-request";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        var item = Assert.Single(workspace.Queue.Items).Item;
        Assert.Equal(agent.Id, item.AgentProfileId);
        Assert.Equal(agent.ModelProfileId, item.Model.Id);
        await fixture.AgentProfiles.SaveAsync(agent with { IsEnabled = !disable, SystemPrompt = "changed-after-enqueue" }, fixture.Token);
        fixture.Server.ReleaseResponses();
        await entered.Task.WaitAsync(fixture.Token);
        try
        {
            Assert.False(workspace.IsRunning);
            Assert.Single(fixture.Server.Requests);
            Assert.Equal(item.Id, Assert.Single((await fixture.Followups.GetAsync(item.TaskId, fixture.Token)).Items).Id);
        }
        finally { release.TrySetResult(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && !workspace.Queue.IsBusy
            && (workspace.Queue.HasNotice || fixture.Server.Requests.Count > 1));
        var saved = await fixture.Tasks.GetMessagesAsync(item.TaskId, fixture.Token);
        if (disable)
        {
            Assert.Single(fixture.Server.Requests);
            Assert.Contains("代理已停用", workspace.Queue.Notice);
            Assert.Single(workspace.Queue.Items);
            Assert.DoesNotContain(saved, message => message.Id == item.Id);
        }
        else
        {
            Assert.Equal(2, fixture.Server.Requests.Count);
            var request = fixture.Server.Requests[1];
            Assert.Equal(fixture.AlternativeModel.ModelId, request.GetProperty("model").GetString());
            Assert.Equal("high", request.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.Contains("saved-agent-instructions", request.GetRawText());
            Assert.DoesNotContain("changed-after-enqueue", request.GetRawText());
            Assert.Empty(workspace.Queue.Items);
            Assert.Single(saved, message => message.Id == item.Id && message.Role == MessageRole.User);
        }
    }
}
