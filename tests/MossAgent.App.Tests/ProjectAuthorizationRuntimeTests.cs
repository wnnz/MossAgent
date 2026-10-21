using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectAuthorizationRuntimeTests
{
    [Fact]
    public async Task SavedScopeChangeStopsItsStreamAndQueueWhileAnotherProjectAndDraftContinue()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        await using var otherServer = new ModelSelectionResponseServer(fixture.Token, pauseResponse: true);
        var workspace = fixture.Workspace;
        var firstProject = workspace.Catalog.SelectedProject!;
        var otherProject = new ProjectProfile(Guid.NewGuid(), "other project", fixture.DataRoot, [fixture.DataRoot], DateTimeOffset.UtcNow);
        await fixture.Tasks.SaveProjectAsync(otherProject, fixture.Token);
        otherProject = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token), project => project.Id == otherProject.Id);
        var alternativeProvider = Assert.Single(await fixture.Configurations.GetProvidersAsync(fixture.Token), item => item.Id == fixture.AlternativeModel.ProviderId);
        await fixture.Configurations.SaveProviderAsync(alternativeProvider with { BaseUri = otherServer.BaseUri }, fixture.Token);
        await workspace.LoadAsync();
        workspace.Catalog.SelectProjectCommand.Execute(firstProject);
        workspace.ComposerText = "first stream";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var firstTask = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "queued message must remain paused";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.Catalog.SelectProjectCommand.Execute(otherProject);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ComposerText = "other project stream";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => otherServer.Requests.Count == 1);
        var otherTask = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "foreground draft remains";

        await fixture.Tasks.SaveProjectAsync(firstProject with { AuthorizedDirectories = [firstProject.PrimaryDirectory, fixture.DataRoot] }, fixture.Token);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(firstTask));
        var failed = Assert.Single(await fixture.Tasks.GetTasksAsync(firstProject.Id, fixture.Token));
        Assert.Equal(TaskFailureReason.WorkspaceAuthorizationChanged, failed.FailureReason);
        Assert.Equal(AgentTaskStatus.Failed, failed.Status);
        Assert.Single((await fixture.Followups.GetAsync(firstTask, fixture.Token)).Items);
        Assert.Single(fixture.Server.Requests);
        Assert.True(fixture.Runs.IsRunning(otherTask));
        Assert.Equal("foreground draft remains", workspace.ComposerText);
        otherServer.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(otherTask));
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        Assert.Equal("foreground draft remains", workspace.ComposerText);
        fixture.Server.ReleaseResponses();
    }

    [Fact]
    public async Task ConcurrentProjectEditRejectsStaleSaveAndRetainsEditorDraft()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var original = fixture.Workspace.Catalog.SelectedProject!;
        var editor = fixture.Workspace.Catalog.ProjectEditor;
        editor.Edit(original);
        editor.Name = "keep local project draft";
        await fixture.Tasks.SaveProjectAsync(original with { AuthorizedDirectories = [original.PrimaryDirectory, fixture.DataRoot] }, fixture.Token);
        await editor.AddCommand.ExecuteAsync(null);
        Assert.True(editor.IsOpen);
        Assert.True(editor.HasStatusError);
        Assert.Contains("授权", editor.Status);
        Assert.Equal("keep local project draft", editor.Name);
        Assert.Equal(original.PrimaryDirectory, Assert.Single(editor.Directories).Path);
        var saved = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        Assert.Equal(2, saved.AuthorizationRevision);
        Assert.Equal(original.Name, saved.Name);
        Assert.Equal(new[] { original.PrimaryDirectory, fixture.DataRoot }, saved.AuthorizedDirectories);
        Assert.Empty(fixture.Server.Requests);
    }
}
