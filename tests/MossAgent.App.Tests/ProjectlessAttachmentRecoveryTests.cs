using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectlessAttachmentRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptSnapshotsKeepTheDraftAndBlockSendingUntilTheChipIsRemoved(bool corrupt)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var picker = new WorkspaceAttachmentOperationProbe();
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, attachmentPicker: picker, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        var path = Path.Combine(fixture.DataRoot, "selected.txt");
        await File.WriteAllTextAsync(path, "selected evidence", fixture.Token);
        picker.Paths = [path];
        await workspace.Attachments.AddFilesCommand.ExecuteAsync(null);
        workspace.ComposerText = "retain this projectless body";
        await workspace.Drafts.FlushAsync();
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();
        File.Delete(path);
        var snapshot = Assert.Single(Directory.GetFiles(Path.Combine(fixture.DataRoot, "draft-attachments"), "*.txt", SearchOption.AllDirectories));
        if (corrupt) await File.WriteAllTextAsync(snapshot, "replaced evidence", fixture.Token);
        else File.Delete(snapshot);
        await using var restarted = AppComposition.CreateServices(fixture.DataRoot);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restarted.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        var chip = Assert.Single(reopened.Attachments.Items);
        Assert.False(chip.IsAvailable);
        Assert.Equal("retain this projectless body", reopened.ComposerText);
        Assert.False(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        Assert.Empty(fixture.Server.Requests);
        chip.RemoveCommand.Execute(null);
        await reopened.Drafts.FlushAsync();
        Assert.True(reopened.PrimaryActionCommand.CanExecute(null));
        reopened.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !reopened.IsRunning);
        Assert.DoesNotContain("selected evidence", fixture.Server.Requests.Single().GetRawText());
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        await reopened.ShutdownAsync();
    }
}
