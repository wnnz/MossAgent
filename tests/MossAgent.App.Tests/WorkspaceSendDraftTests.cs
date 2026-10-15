using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSendDraftTests
{
    [Fact]
    public async Task FailedPreparationPreservesDraftAndAttachmentsWithoutCallingProvider()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, _) => Task.FromException(new InvalidOperationException("Authorization: Bearer synthetic-secret"))
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        const string draft = "  保留这段草稿和附件。  ";
        workspace.ComposerText = draft;
        var attachment = AddAttachment(workspace, "first.txt");

        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);

        Assert.Equal(draft, workspace.ComposerText);
        Assert.Same(attachment, Assert.Single(workspace.Attachments.Items));
        Assert.Empty(fixture.Server.Requests);
        Assert.DoesNotContain("synthetic-secret", workspace.Activity);
        Assert.True(workspace.HasSendPreparationError);
        Assert.DoesNotContain("synthetic-secret", workspace.SendPreparationError);
        Assert.Equal("发送消息", workspace.PrimaryActionLabel);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
    }

    [Fact]
    public async Task PendingPreparationRetainsOriginalDraftAndOnlyConsumesTheCapturedAttachments()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => release.Task };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "发送第一条消息。";
        var first = AddAttachment(workspace, "first.txt");
        workspace.PrimaryActionCommand.Execute(null);

        Assert.Equal("发送第一条消息。", workspace.ComposerText);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.False(workspace.HasActiveRun);
        Assert.Equal("正在保存消息", workspace.PrimaryActionLabel);
        workspace.ComposerText = "为下一条消息准备的新草稿。";
        var next = AddAttachment(workspace, "next.txt");
        release.SetResult();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);

        Assert.Equal("为下一条消息准备的新草稿。", workspace.ComposerText);
        Assert.Same(next, Assert.Single(workspace.Attachments.Items));
        Assert.DoesNotContain(first, workspace.Attachments.Items);
        Assert.False(workspace.HasSendPreparationError);
    }

    private static WorkspaceAttachmentViewModel AddAttachment(WorkspaceViewModel workspace, string path)
    {
        var attachment = new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(
            Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, path), path, "synthetic content", 17), _ => { });
        workspace.Attachments.Items.Add(attachment);
        return attachment;
    }

    [Fact]
    public async Task CancellingActiveTaskPreservesNewDraftAttachmentsUntilRealNavigation()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        Assert.True(workspace.HasActiveRun);
        Assert.Equal("停止执行", workspace.PrimaryActionLabel);
        workspace.ComposerText = "下一条消息的草稿。";
        var attachment = AddAttachment(workspace, "next.txt");

        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);

        Assert.Equal("下一条消息的草稿。", workspace.ComposerText);
        Assert.Same(attachment, Assert.Single(workspace.Attachments.Items));
        workspace.Catalog.NewTaskCommand.Execute(null);
        Assert.Empty(workspace.Attachments.Items);
        Assert.False(workspace.HasActiveRun);
    }

    [Fact]
    public async Task AttachmentCollectionChangesNotifyVisibilityAndDisposedManagerCannotOpenPicker()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var manager = fixture.Workspace.Attachments;
        var notifications = 0;
        manager.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(manager.HasItems)) notifications++; };
        var attachment = AddAttachment(fixture.Workspace, "next.txt");
        Assert.True(manager.HasItems);
        Assert.Equal(1, notifications);
        manager.Consume([attachment]);
        Assert.False(manager.HasItems);
        Assert.Equal(2, notifications);

        manager.Dispose();
        manager.Dispose();
        Assert.False(manager.AddFilesCommand.CanExecute(fixture.Workspace.Catalog.SelectedProject));
        await manager.AddFilesCommand.ExecuteAsync(fixture.Workspace.Catalog.SelectedProject);
        manager.Items.Add(attachment);
        Assert.Equal(2, notifications);
    }
}
