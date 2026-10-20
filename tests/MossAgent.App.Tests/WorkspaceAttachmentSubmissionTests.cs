using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentSubmissionTests
{
    [Fact]
    public async Task SelectedAuthorizedTextReachesActualProviderRequestAndVisibleMessage()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "selected-context.txt");
        const string marker = "actual-authorized-attachment-context";
        await File.WriteAllTextAsync(path, marker, fixture.Token);
        picker.Paths = [path];

        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var attachment = Assert.Single(workspace.Attachments.Items);
        Assert.Equal(path, attachment.FullPath);
        workspace.ComposerText = "请检查刚选择的文本附件。";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);

        var request = Assert.Single(fixture.Server.Requests).GetRawText();
        Assert.Contains(marker, request);
        Assert.Contains("workspace_attachments", request);
        var visible = Assert.Single(workspace.Messages, message => message.IsUserMessage);
        Assert.Single(visible.Attachments);
        Assert.Empty(workspace.Attachments.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        var stored = await fixture.Tasks.GetMessagesAsync(workspace.Catalog.SelectedTask.Id, fixture.Token);
        Assert.Contains(stored, message => message.Role == MessageRole.Assistant);
    }

    [Fact]
    public async Task ForeignFileIsDeniedByActualReaderAndNeverAppearsInProviderRequest()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        picker.Paths = [Path.Combine(project.PrimaryDirectory, "..", "synthetic-foreign-attachment.txt")];

        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);

        Assert.Empty(workspace.Attachments.Items);
        Assert.Equal(WorkspaceText.Get("AttachmentUnauthorized"), workspace.Attachments.Status);
        workspace.ComposerText = "附件被拒绝后仍可正常发送。";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var request = Assert.Single(fixture.Server.Requests).GetRawText();
        Assert.DoesNotContain("synthetic-foreign-attachment", request);
        Assert.DoesNotContain("workspace_attachments", request);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownCancelsPendingAttachmentPortsWithoutWaitingForTheirCooperation(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: probe, attachmentReader: probe);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        probe.SetContent(project);
        var adding = workspace.Attachments.AddFilesCommand.ExecuteAsync(project);

        await workspace.ShutdownAsync().WaitAsync(fixture.Token);
        await adding.WaitAsync(fixture.Token);
        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);
        Assert.False(workspace.Attachments.AddFilesCommand.CanExecute(project));
        var pickerCalls = probe.PickerCalls;
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        Assert.Equal(pickerCalls, probe.PickerCalls);
        probe.Release();
        Assert.Empty(workspace.Attachments.Items);
        Assert.Empty(fixture.Server.Requests);
    }
}
