using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentHistoryFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrTamperedSnapshotBlocksProviderUntilExplicitRepair(bool missing)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var source = Path.Combine(project.PrimaryDirectory, "history-failure.txt");
        const string content = "original snapshot data";
        await File.WriteAllTextAsync(source, content, fixture.Token);
        picker.Paths = [source];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "first turn";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var task = workspace.Catalog.SelectedTask!;
        var original = Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.User);
        var attachment = Assert.Single(original.Attachments!);
        var path = Path.Combine(fixture.DataRoot, "artifacts", task.Id.ToString("N"), "attachments",
            original.Id.ToString("N"), attachment.Id.ToString("N") + ".txt");
        if (missing) File.Delete(path);
        else await File.WriteAllTextAsync(path, new string('x', content.Length), fixture.Token);

        workspace.ComposerText = "must not reach the provider with missing history";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(TaskFailureReason.AttachmentHistory, workspace.Catalog.SelectedTask.FailureReason);
        Assert.Equal(WorkspaceText.Get("TaskFailureAttachmentHistory"), workspace.TaskFailureHint);
        Assert.DoesNotContain(path, workspace.Activity);
        Assert.Equal(TaskFailureReason.AttachmentHistory,
            Assert.Single(await fixture.Tasks.GetTasksAsync(project.Id, fixture.Token)).FailureReason);

        await File.WriteAllTextAsync(path, content, fixture.Token);
        workspace.ComposerText = "manual retry after restoring the snapshot";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        Assert.Null(workspace.Catalog.SelectedTask.FailureReason);
        Assert.Contains(content, fixture.Server.Requests.Last().GetRawText());
        Assert.Equal(attachment, Assert.Single((await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token))
            .Single(message => message.Id == original.Id).Attachments!));
    }
}
