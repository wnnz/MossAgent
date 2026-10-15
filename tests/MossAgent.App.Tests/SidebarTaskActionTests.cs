using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarTaskActionTests
{
    [Fact]
    public async Task RenameMenu_UsesItsRowWithoutChangingSelection()
    {
        var repository = new TestWorkspaceRepository();
        var selected = CreateTask("open conversation");
        var target = CreateTask("menu conversation");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = selected };

        management.BeginRenameCommand.Execute(target);
        Assert.Equal(target.Title, management.EditingTitle);
        Assert.Same(selected, management.SelectedTask);
        management.EditingTitle = "renamed row";
        await management.SaveRenameCommand.ExecuteAsync(null);

        Assert.Equal(target.Id, repository.SavedTask?.Id);
        Assert.Equal("renamed row", repository.SavedTask?.Title);
        Assert.Same(selected, management.SelectedTask);
        Assert.False(management.IsEditing);
    }

    [Fact]
    public async Task ArchiveMenu_UsesItsRowWithoutChangingSelection()
    {
        var repository = new TestWorkspaceRepository();
        var selected = CreateTask("open conversation");
        var target = CreateTask("menu conversation");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = selected };
        AgentTask? archived = null;
        management.TaskArchived += task => archived = task;
        await management.LoadAsync(selected.ProjectId);

        await management.ArchiveCommand.ExecuteAsync(target);

        Assert.Equal([(target.Id, true)], repository.ArchiveOperations);
        Assert.Same(target, archived);
        Assert.Same(selected, management.SelectedTask);
        Assert.Empty(management.ArchivedTasks);
    }

    [Fact]
    public async Task PinMenu_UsesTheRowsOwnPinState()
    {
        var repository = new TestWorkspaceRepository();
        var selected = CreateTask("open conversation");
        var target = CreateTask("pinned menu conversation") with { IsPinned = true };
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = selected };

        await management.TogglePinCommand.ExecuteAsync(target);

        Assert.Equal([(target.Id, false)], repository.PinOperations);
        Assert.Same(selected, management.SelectedTask);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MenuAvailability_UsesTheRowsRunState(bool targetRunning)
    {
        var selected = CreateTask("open conversation");
        var target = CreateTask("menu conversation");
        var management = new WorkspaceTaskManagementViewModel(new TestWorkspaceRepository())
        {
            SelectedTask = selected,
            IsTaskRunning = taskId => taskId == (targetRunning ? target.Id : selected.Id)
        };

        Assert.Equal(!targetRunning, management.BeginRenameCommand.CanExecute(target));
        Assert.Equal(!targetRunning, management.ArchiveCommand.CanExecute(target));
        Assert.True(management.TogglePinCommand.CanExecute(target));
    }

    [Fact]
    public async Task RunningRow_CannotBeModifiedByDirectCommandExecution()
    {
        var repository = new TestWorkspaceRepository();
        var task = CreateTask("running");
        var management = new WorkspaceTaskManagementViewModel(repository)
        {
            SelectedTask = task,
            IsTaskRunning = _ => true
        };

        management.BeginRenameCommand.Execute(task);
        await management.ArchiveCommand.ExecuteAsync(task);

        Assert.False(management.IsEditing);
        Assert.Empty(repository.ArchiveOperations);
    }

    [Fact]
    public async Task RenameSave_RechecksTheTargetRunState()
    {
        var repository = new TestWorkspaceRepository();
        var selected = CreateTask("open conversation");
        var target = CreateTask("menu conversation");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = selected };
        management.BeginRenameCommand.Execute(target);
        management.EditingTitle = "updated title";
        management.IsTaskRunning = taskId => taskId == target.Id;
        management.NotifyTaskRunStateChanged();

        Assert.False(management.SaveRenameCommand.CanExecute(null));
        await management.SaveRenameCommand.ExecuteAsync(null);

        Assert.Null(repository.SavedTask);
        Assert.Same(selected, management.SelectedTask);
    }

    [Fact]
    public async Task RenameMenu_UpdatesTheCatalogWithoutSelectingAnotherConversation()
    {
        var repository = new TestWorkspaceRepository();
        var selected = CreateTask("open conversation");
        var target = CreateTask("menu conversation") with { ProjectId = selected.ProjectId };
        var project = new ProjectProfile(Assert.IsType<Guid>(selected.ProjectId), "fixture", "D:\\fixture",
            ["D:\\fixture"], DateTimeOffset.UtcNow);
        repository.ProjectItems = [project];
        repository.TasksByProject[project.Id] = [selected, target];
        var catalog = new WorkspaceCatalogViewModel(repository,
            new WorkspaceWorktreeCleanupService(repository, null!),
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        await catalog.ReloadAsync();
        catalog.UpsertTask(selected);

        catalog.TaskManagement.BeginRenameCommand.Execute(target);
        catalog.TaskManagement.EditingTitle = "renamed row";
        await catalog.TaskManagement.SaveRenameCommand.ExecuteAsync(null);

        Assert.Equal(selected.Id, catalog.SelectedTask?.Id);
        var group = Assert.Single(catalog.ProjectNavigation);
        Assert.Equal("renamed row", Assert.Single(group.Tasks,
            item => item.Task.Id == target.Id).Task.Title);
    }

    private static AgentTask CreateTask(string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(Guid.NewGuid(), Guid.NewGuid(), title, ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
    }
}
