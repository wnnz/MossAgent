using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AttachmentHistoryThumbnailWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SentAndReopenedHistoryThumbnailsUseImmutableSnapshotsWithoutTheSelectedFile(bool projectless)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: !projectless);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var editor = fixture.Main.Models;
        editor.EditProviderModelCommand.Execute(fixture.SelectedModel);
        editor.SupportsImages = true;
        await editor.SaveModelCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var path = Path.Combine(Path.GetDirectoryName(fixture.DataRoot)!, "history-thumbnail.png");
        var image = ThumbnailTestRenderer.Image;
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(image.Base64Data), fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(workspace.Catalog.SelectedProject);
        var selected = Assert.Single(workspace.Attachments.Items);
        var selectedSource = Assert.IsType<AttachmentPreviewSource>(selected.Thumbnail);
        workspace.ComposerText = "sent image evidence";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var sent = Assert.Single(workspace.Messages.First(message => message.IsUserMessage).Attachments);
        Assert.Same(selectedSource, sent.Thumbnail);
        Assert.Empty(workspace.Attachments.Items);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        Assert.Equal(1, (await selectedSource.LoadAsync(fixture.Token)).Width);
        Assert.Equal(1, (await selectedSource.LoadImageAsync(fixture.Token)).Width);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectTaskCommand.Execute(reopened.Catalog.Tasks.Single(task => task.Id == taskId));
        await fixture.WaitUntilAsync(() => reopened.Messages.Any(message => message.IsUserMessage));
        var restoredMessage = reopened.Messages.First(message => message.IsUserMessage);
        var restored = Assert.Single(restoredMessage.Attachments);
        var restoredSource = Assert.IsType<AttachmentPreviewSource>(restored.Thumbnail);
        var thumbnail = await restoredSource.LoadAsync(fixture.Token);
        var fullImage = await restoredSource.LoadImageAsync(fixture.Token);

        Assert.Equal("image/png", thumbnail.MediaType);
        Assert.Equal(1, thumbnail.Width);
        Assert.Equal(1, thumbnail.Height);
        Assert.Equal(1, fullImage.Width);
        Assert.Equal(1, fullImage.Height);
        Assert.Equal("sent image evidence", restoredMessage.Content);
        Assert.False(File.Exists(path));
        Assert.Equal(projectless, reopened.Catalog.SelectedProject is null);
        if (projectless) Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        Assert.Single(fixture.Server.Requests);
        await reopened.ShutdownAsync();
    }
}
