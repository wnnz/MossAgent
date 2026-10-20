using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentDraftPromotionTests
{
    [Fact]
    public async Task ReselectingSameSourceDuringPreparationKeepsNewIdentityAndRestoresOnlyNextDraft()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        var repository = new PausingWorkspaceRepository(fixture.Tasks);
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var project = workspace.Catalog.SelectedProject!;
        var source = workspace.Drafts.Scope;
        var path = Path.Combine(project.PrimaryDirectory, "reselected.txt");
        await File.WriteAllTextAsync(path, "first captured attachment", fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var captured = Assert.Single(workspace.Attachments.Items);
        workspace.ComposerText = "send first message";
        await workspace.Drafts.FlushAsync();
        workspace.PrimaryActionCommand.Execute(null);
        await repository.Entered.Task.WaitAsync(fixture.Token);

        captured.RemoveCommand.Execute(null);
        await File.WriteAllTextAsync(path, "next unsent attachment", fixture.Token);
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(project);
        var next = Assert.Single(workspace.Attachments.Items);
        Assert.NotEqual(captured.SelectionId, next.SelectionId);
        workspace.ComposerText = "next unsent body";
        repository.Release.SetResult();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        Assert.Same(next, Assert.Single(workspace.Attachments.Items));
        Assert.Contains("first captured attachment", fixture.Server.Requests.Single().GetRawText());
        Assert.DoesNotContain("next unsent attachment", fixture.Server.Requests.Single().GetRawText());
        await workspace.Drafts.FlushAsync();
        var task = workspace.Catalog.SelectedTask!;
        Assert.Empty((await fixture.Drafts.GetAsync(source, fixture.Token))!.AttachmentItems);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);

        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectedTask = task;
        await reopened.Drafts.LoadCurrentAsync();
        var restored = Assert.Single(reopened.Attachments.Items);
        Assert.Equal(next.SelectionId, restored.SelectionId);
        Assert.Equal("next unsent attachment", restored.Content.Content);
        Assert.Equal("next unsent body", reopened.ComposerText);
        Assert.Single(fixture.Server.Requests);
        await reopened.ShutdownAsync();
    }
}
