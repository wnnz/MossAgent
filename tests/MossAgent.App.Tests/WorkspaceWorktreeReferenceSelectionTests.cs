using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeReferenceSelectionTests
{
    [Fact]
    public void LockAndExistingTaskCannotChangeReferenceAndContextSwitchResetsIt()
    {
        var project = new ProjectProfile(Guid.NewGuid(), "project", "root", ["root"], DateTimeOffset.UtcNow);
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.BaseReference = "no-project";
        Assert.Empty(environment.BaseReference);
        environment.SetContext(project, null);
        environment.BaseReference = "selected-base";
        environment.UseWorktreeCommand.Execute(null);
        environment.SetLocked(true);
        environment.BaseReference = "late-change";
        Assert.Equal("selected-base", environment.BaseReference);
        environment.SetLocked(false);
        environment.SetContext(project, null);
        Assert.Equal("selected-base", environment.BaseReference);
        var now = DateTimeOffset.UtcNow;
        environment.SetContext(project, new(Guid.NewGuid(), project.Id, "saved", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null));
        environment.BaseReference = "existing-change";
        Assert.Empty(environment.BaseReference);
        environment.SetContext(null, null);
        Assert.Empty(environment.BaseReference);
    }

    [Fact]
    public async Task CapturedRequestRetainsReferenceAndProjectDirectoryIgnoresUnusedField()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        workspace.Catalog.Environment.BaseReference = " selected-base ";
        Assert.Null(WorkspaceSendRequest.Capture(workspace).WorktreeBaseReference);
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        var captured = WorkspaceSendRequest.Capture(workspace);
        workspace.Catalog.Environment.BaseReference = "later-base";
        Assert.Equal("selected-base", captured.WorktreeBaseReference);
        Assert.Equal("later-base", WorkspaceSendRequest.Capture(workspace).WorktreeBaseReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryRefusesReferenceOverrideWithoutNewWorktree(bool existing)
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectProfile(Guid.NewGuid(), "project", "root", ["root"], now);
        var task = existing ? new AgentTask(Guid.NewGuid(), project.Id, "saved", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null) : null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.PrepareAsync(project, task, "prompt",
            ApprovalPolicy.ReadOnly, [], TestContext.Current.CancellationToken, baseReference: "selected-base"));
        Assert.Empty(probe.Created);
        Assert.Null(repository.SavedTask);
    }
}
