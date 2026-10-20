using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectlessAttachmentIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NavigationInvalidatesPendingProjectlessPickerOrReaderAndCannotPublishLateFiles(bool duringRead)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var picker = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead };
        var selected = new SelectedAttachmentOperationProbe { Hold = true };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, selectedAttachmentReader: selected);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        var path = Path.Combine(fixture.DataRoot, "late.txt");
        picker.Paths = [path];
        selected.Contents = [new(path, "late.txt", "must not leak into project", 26)];
        var adding = workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        if (duringRead) await fixture.WaitUntilAsync(() => selected.Calls == 1);
        workspace.Catalog.SelectedProject = project;
        await adding.WaitAsync(fixture.Token);
        Assert.True(duringRead ? selected.Token.IsCancellationRequested : picker.PickerToken.IsCancellationRequested);
        picker.Release();
        selected.Completion.TrySetResult(selected.Contents);
        Assert.Empty(workspace.Attachments.Items);
        Assert.Empty(fixture.Server.Requests);
        Assert.Equal(project.Id, workspace.Catalog.SelectedProject?.Id);
        await workspace.ShutdownAsync();
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("foreign")]
    [InlineData("missing")]
    public async Task RelativeSelectionsAndForeignOrIncompletePortResultsCannotBecomeAuthorizedChips(string kind)
    {
        var picker = new WorkspaceAttachmentOperationProbe();
        var selected = new SelectedAttachmentOperationProbe();
        var original = Path.Combine(Path.GetTempPath(), "MossAgent", "selected.txt");
        picker.Paths = [kind == "relative" ? "selected.txt" : original];
        selected.Contents = kind == "missing" ? [] : [new(original + ".foreign", "foreign.txt", "not selected", 12)];
        using var attachments = new WorkspaceAttachmentManagerViewModel(picker, picker, selectedReader: selected);
        await attachments.AddFilesCommand.ExecuteAsync(null);
        Assert.Equal(kind == "relative" ? 0 : 1, selected.Calls);
        Assert.Equal(0, picker.ReaderCalls);
        Assert.Empty(attachments.Items);
        Assert.Equal(WorkspaceText.Get("AttachmentUnauthorized"), attachments.Status);
    }
}
