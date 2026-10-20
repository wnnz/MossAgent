using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskEnvironmentTests
{
    [Fact]
    public void Projectless_CannotChooseOrOpenPicker()
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.IsPickerOpen = true;
        environment.UseWorktreeCommand.Execute(null);
        Assert.False(environment.HasProject);
        Assert.False(environment.CanChoose);
        Assert.False(environment.IsPickerOpen);
        Assert.False(environment.UseWorktree);
        Assert.False(environment.UseWorktreeCommand.CanExecute(null));
    }

    [Fact]
    public void NewProjectConversation_ExplicitChoiceUpdatesLabelAndClosesPicker()
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(CreateProject(), null);
        Assert.True(environment.CanChoose);
        Assert.Equal(environment.ProjectDirectoryLabel, environment.Label);
        environment.IsPickerOpen = true;
        environment.UseWorktreeCommand.Execute(null);
        Assert.True(environment.UseWorktree);
        Assert.True(environment.IsWorktree);
        Assert.Equal(environment.WorktreeLabel, environment.Label);
        Assert.False(environment.IsPickerOpen);
        environment.UseProjectDirectoryCommand.Execute(null);
        Assert.False(environment.UseWorktree);
        Assert.False(environment.IsWorktree);
    }

    [Fact]
    public void Lock_ClosesPickerAndRejectsCommandsWithoutDiscardingChoice()
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(CreateProject(), null);
        environment.UseWorktreeCommand.Execute(null);
        environment.IsPickerOpen = true;
        environment.SetLocked(true);
        environment.UseProjectDirectoryCommand.Execute(null);
        environment.IsPickerOpen = true;
        Assert.False(environment.CanChoose);
        Assert.False(environment.IsPickerOpen);
        Assert.False(environment.UseProjectDirectoryCommand.CanExecute(null));
        Assert.True(environment.UseWorktree);
        environment.SetLocked(false);
        Assert.True(environment.CanChoose);
        Assert.True(environment.UseWorktree);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingConversation_DisplaysPersistedEnvironmentAndCannotSwitch(bool worktree)
    {
        var project = CreateProject();
        var environment = new WorkspaceTaskEnvironmentViewModel();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "existing", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, worktree ? "managed-worktree" : null);
        environment.SetContext(project, task);
        environment.UseWorktreeCommand.Execute(null);
        environment.UseProjectDirectoryCommand.Execute(null);
        Assert.True(environment.HasProject);
        Assert.False(environment.CanChoose);
        Assert.False(environment.UseWorktree);
        Assert.Equal(worktree, environment.IsWorktree);
        Assert.Equal(worktree ? environment.WorktreeLabel : environment.ProjectDirectoryLabel, environment.Label);
    }

    [Fact]
    public void ContextChange_ResetsChoiceButRepeatedContextDoesNot()
    {
        var project = CreateProject();
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(project, null);
        environment.UseWorktreeCommand.Execute(null);
        environment.SetContext(project, null);
        Assert.True(environment.UseWorktree);
        environment.SetContext(project with { Id = Guid.NewGuid() }, null);
        Assert.False(environment.UseWorktree);
        environment.UseWorktreeCommand.Execute(null);
        environment.SetContext(null, null);
        Assert.False(environment.IsWorktree);
        Assert.False(environment.HasProject);
    }

    [Fact]
    public void RecoveryNotice_ContainsManagedPathButNotRawFailure()
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(CreateProject(), null);
        environment.ReportPreparationFailure(new WorkspaceWorktreePreparationException("managed-worktree",
            new IOException("synthetic-secret-do-not-display")));
        Assert.True(environment.HasPreparationNotice);
        Assert.Contains("managed-worktree", environment.PreparationNotice);
        Assert.DoesNotContain("synthetic-secret", environment.PreparationNotice);
        environment.UseProjectDirectoryCommand.Execute(null);
        Assert.False(environment.HasPreparationNotice);
    }

    private static ProjectProfile CreateProject() => new(Guid.NewGuid(), "project", "project-root",
        ["project-root"], DateTimeOffset.UtcNow);
}
