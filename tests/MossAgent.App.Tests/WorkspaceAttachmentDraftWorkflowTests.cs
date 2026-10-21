using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentDraftWorkflowTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("authorization")]
    public async Task InvalidRestoredAttachmentCannotReachProviderAndRemovalPreservesEditedText(string damage)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "blocked-draft.txt");
        const string marker = "attachment-must-never-be-transmitted";
        await File.WriteAllTextAsync(path, marker, fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        workspace.ComposerText = "saved before restart";
        await workspace.Drafts.FlushAsync();
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        var snapshot = Assert.Single(Directory.GetFiles(Path.Combine(fixture.DataRoot, "draft-attachments"), "*.txt", SearchOption.AllDirectories));
        if (damage == "missing") File.Delete(snapshot);
        else if (damage == "hash") await File.WriteAllTextAsync(snapshot, "X" + marker[1..], fixture.Token);
        else await fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = [] }, fixture.Token);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        var chip = Assert.Single(reopened.Attachments.Items);
        Assert.Equal(damage == "authorization", chip.IsAvailable);
        Assert.False(reopened.PrimaryActionCommand.CanExecute(null));
        Assert.True(reopened.Drafts.HasNotice);
        reopened.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        reopened.ComposerText = "preserve new text after removing unavailable attachment";
        if (damage != "authorization") await Assert.ThrowsAsync<InvalidOperationException>(reopened.Drafts.FlushAsync);
        chip.RemoveCommand.Execute(null);
        await reopened.Drafts.FlushAsync();
        if (damage == "authorization")
        {
            var revoked = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token));
            Assert.Empty(revoked.RootAuthorizations);
            await fixture.Tasks.SaveProjectAsync(revoked with { AuthorizedDirectories = [revoked.PrimaryDirectory] }, fixture.Token);
            var authorized = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token));
            Assert.Single(authorized.RootAuthorizations);
            reopened.Catalog.SelectedProject = authorized;
            await reopened.Drafts.LoadCurrentAsync();
            Assert.Equal("preserve new text after removing unavailable attachment", reopened.ComposerText);
        }
        Assert.True(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        var request = fixture.Server.Requests.Single().GetRawText();
        Assert.Contains("preserve new text", request);
        Assert.DoesNotContain(marker, request);
        Assert.DoesNotContain("workspace_attachments", request);
        await reopened.ShutdownAsync();
    }

    [Fact]
    public async Task UnsentAttachmentAndTextSurviveNavigationAndIndependentCompositionWithoutSourceReread()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var path = Path.Combine(project.PrimaryDirectory, "unsent-draft.txt");
        const string marker = "original-unsent-attachment-content";
        await File.WriteAllTextAsync(path, marker, fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var originalDisplayPath = Assert.Single(workspace.Attachments.Items).DisplayPath;
        workspace.ComposerText = "尚未发送的正文";
        await workspace.Drafts.FlushAsync();

        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Empty(workspace.Attachments.Items);
        Assert.Empty(workspace.ComposerText);
        workspace.Catalog.SelectedProject = project;
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Single(workspace.Attachments.Items);
        Assert.Equal("尚未发送的正文", workspace.ComposerText);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        var attachment = Assert.Single(reopened.Attachments.Items);
        Assert.Equal(originalDisplayPath, attachment.DisplayPath);
        Assert.Equal(marker, attachment.Content.Content);
        Assert.Equal("尚未发送的正文", reopened.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        Assert.Contains(marker, fixture.Server.Requests.Single().GetRawText());
        Assert.Empty(reopened.Attachments.Items);
        await reopened.ShutdownAsync();
    }
}
