using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSubmissionRaceTests
{
    [Fact]
    public async Task TypingAndReselectionDuringCommitPreserveOnlyNextInputAcrossReopen()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var repository = new SubmissionWorkspaceRepository(fixture.Tasks);
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs, conversationDrafts: fixture.Drafts, attachmentPicker: picker);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var source = workspace.Drafts.Scope;
        var path = Path.Combine(project.PrimaryDirectory, "atomic.txt");
        await File.WriteAllTextAsync(path, "sent attachment", fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var sent = Assert.Single(workspace.Attachments.Items);
        workspace.ComposerText = "sent body";
        await workspace.Drafts.FlushAsync();
        workspace.PrimaryActionCommand.Execute(null);
        await repository.Entered.Task.WaitAsync(fixture.Token);
        sent.RemoveCommand.Execute(null);
        await File.WriteAllTextAsync(path, "next attachment", fixture.Token);
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var next = Assert.Single(workspace.Attachments.Items);
        workspace.ComposerText = "next body";
        repository.Release.TrySetResult();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        await workspace.Drafts.FlushAsync();
        Assert.Same(next, Assert.Single(workspace.Attachments.Items));
        Assert.Equal("next body", workspace.ComposerText);
        Assert.Empty((await fixture.Drafts.GetAsync(source, fixture.Token))!.Text);
        Assert.Empty((await fixture.Drafts.GetAsync(source, fixture.Token))!.AttachmentItems);
        var task = workspace.Catalog.SelectedTask!;
        var message = Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), item => item.Role == MessageRole.User);
        Assert.Equal("sent body", message.Content);
        Assert.Single(message.Attachments!);
        Assert.Contains("sent attachment", fixture.Server.Requests.Single().GetRawText());
        Assert.DoesNotContain("next attachment", fixture.Server.Requests.Single().GetRawText());
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectedTask = task;
        await reopened.Drafts.LoadCurrentAsync();
        Assert.Equal("next body", reopened.ComposerText);
        Assert.Equal(next.SelectionId, Assert.Single(reopened.Attachments.Items).SelectionId);
        Assert.Equal("next attachment", reopened.Attachments.Items[0].Content.Content);
        Assert.Single(fixture.Server.Requests);
        await reopened.ShutdownAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentEditorConflictCannotCommitOrReachTheProvider(bool withProject)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var repository = new SubmissionWorkspaceRepository(fixture.Tasks);
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        workspace.ComposerText = "local input";
        await workspace.Drafts.FlushAsync();
        var scope = workspace.Drafts.Scope;
        var saved = (await fixture.Drafts.GetAsync(scope, fixture.Token))!;
        workspace.PrimaryActionCommand.Execute(null);
        await repository.Entered.Task.WaitAsync(fixture.Token);
        Assert.True(await fixture.Drafts.SaveAsync([new(scope, "another editor", saved.Revision)], fixture.Token));
        repository.Release.TrySetResult();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(workspace.HasSendPreparationError);
        Assert.True(workspace.Drafts.IsConflict);
        Assert.Equal("local input", workspace.ComposerText);
        Assert.Equal("another editor", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(scope.ProjectId, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
        await workspace.Drafts.KeepLocalCommand.ExecuteAsync(null);
        Assert.Equal("local input", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
        Assert.Empty(fixture.Server.Requests);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
    }
}
