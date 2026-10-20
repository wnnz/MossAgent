using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NavigationDuringSelectionCannotAttachOldProjectContentToPlainChat(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: probe, attachmentReader: probe);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        probe.SetContent(project);
        var adding = workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        Assert.Equal(duringRead ? 1 : 0, probe.ReaderCalls);

        workspace.Catalog.NewChatCommand.Execute(null);
        Assert.Null(workspace.Catalog.SelectedProject);
        probe.Release();
        await adding.WaitAsync(fixture.Token);

        Assert.Empty(workspace.Attachments.Items);
        Assert.Null(workspace.Attachments.BuildContext());
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "普通聊天不能带入上个项目的附件。";
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var request = Assert.Single(fixture.Server.Requests).GetRawText();
        Assert.DoesNotContain("synthetic-attachment-owner-marker", request);
        Assert.DoesNotContain("workspace_attachments", request);
        Assert.Null(workspace.Catalog.SelectedTask!.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearingSelectionCancelsPendingPortAndRejectsLateResults(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: probe, attachmentReader: probe);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        probe.SetContent(project);
        var adding = workspace.Attachments.AddFilesCommand.ExecuteAsync(project);

        workspace.Attachments.Clear();
        probe.Release();
        await adding.WaitAsync(fixture.Token);

        Assert.Empty(workspace.Attachments.Items);
        Assert.Empty(workspace.Attachments.Status);
        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);
        Assert.True(workspace.Attachments.AddFilesCommand.CanExecute(project));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalCancelsPendingPortWithoutPublishingContentOrErrors(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: probe, attachmentReader: probe);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        probe.SetContent(project);
        var adding = workspace.Attachments.AddFilesCommand.ExecuteAsync(project);

        workspace.Dispose();
        probe.Release();
        await adding.WaitAsync(fixture.Token);

        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);
        Assert.Empty(workspace.Attachments.Items);
        Assert.Empty(workspace.Attachments.Status);
        Assert.False(workspace.Attachments.AddFilesCommand.CanExecute(project));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PickerAndReaderFailuresDoNotExposeRawExceptionMessages(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var error = new InvalidOperationException("Authorization: Bearer synthetic-secret-value");
        var probe = new WorkspaceAttachmentOperationProbe
        {
            PickerError = duringRead ? null : error,
            ReaderError = duringRead ? error : null
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: probe, attachmentReader: probe);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        probe.SetContent(project);

        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);

        Assert.NotEmpty(workspace.Attachments.Status);
        Assert.DoesNotContain("synthetic-secret-value", workspace.Attachments.Status);
        Assert.DoesNotContain("Authorization", workspace.Attachments.Status);
        Assert.Empty(workspace.Attachments.Items);
    }
}
