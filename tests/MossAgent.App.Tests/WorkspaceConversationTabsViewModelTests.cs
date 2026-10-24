using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceConversationTabsViewModelTests
{
    [Fact]
    public async Task TabsSwitchAcrossProjectsAndCloseWithoutArchiving()
    {
        var repository = new TestWorkspaceRepository();
        var firstProject = Project("First");
        var secondProject = Project("Second");
        var firstTask = CreateTask(firstProject, "First task");
        var secondTask = CreateTask(secondProject, "Second task");
        repository.ProjectItems = [firstProject, secondProject];
        repository.TasksByProject[firstProject.Id] = [firstTask];
        repository.TasksByProject[secondProject.Id] = [secondTask];
        var catalog = Catalog(repository);
        await catalog.ReloadAsync();
        var tabs = new WorkspaceConversationTabsViewModel(catalog);

        Assert.Equal(firstTask.Id, Assert.Single(tabs.Items).Task.Id);
        catalog.SelectTaskCommand.Execute(secondTask);
        Assert.Equal(secondTask.Id, catalog.SelectedTask?.Id);
        Assert.Equal(2, tabs.Items.Count);
        Assert.True(tabs.Items[1].IsSelected);
        Assert.Same(tabs.Items[1], tabs.SelectedTab);

        tabs.SelectTabCommand.Execute(tabs.Items[0]);
        Assert.Equal(firstTask.Id, catalog.SelectedTask?.Id);
        Assert.Same(tabs.Items[0], tabs.SelectedTab);
        tabs.CloseTabCommand.Execute(tabs.Items[0]);
        Assert.Equal(secondTask.Id, catalog.SelectedTask?.Id);
        Assert.Single(tabs.Items);
        Assert.Same(tabs.Items[0], tabs.SelectedTab);
        Assert.Empty(repository.ArchiveOperations);

        tabs.CloseTabCommand.Execute(tabs.Items[0]);
        Assert.Null(catalog.SelectedTask);
        Assert.Empty(tabs.Items);
        Assert.Null(tabs.SelectedTab);
    }

    [Fact]
    public async Task TabsFollowRenamesAndArchivingWithoutRestoringClosedTabs()
    {
        var repository = new TestWorkspaceRepository();
        var project = Project("Workspace");
        var task = CreateTask(project, "Before");
        repository.ProjectItems = [project];
        repository.TasksByProject[project.Id] = [task];
        var catalog = Catalog(repository);
        await catalog.ReloadAsync();
        var tabs = new WorkspaceConversationTabsViewModel(catalog);

        catalog.UpsertTask(task with { Title = "After" });
        Assert.Equal("After", Assert.Single(tabs.Items).Title);
        catalog.IsLocked = true;
        Assert.False(tabs.CloseTabCommand.CanExecute(tabs.Items[0]));
        catalog.IsLocked = false;
        await catalog.TaskManagement.ArchiveCommand.ExecuteAsync(null);
        Assert.Empty(tabs.Items);
        Assert.Null(catalog.SelectedTask);
        Assert.Single(repository.ArchiveOperations);
        Assert.Null(tabs.SelectedTab);
    }

    private static WorkspaceCatalogViewModel Catalog(TestWorkspaceRepository repository) => new(
        repository, null,
        new WorkspaceProjectService(repository, new TestFolderPickerService()));

    [Fact]
    public async Task DisposedTabs_StopObservingAndRejectStaleNavigationWithoutArchiving()
    {
        var repository = new TestWorkspaceRepository();
        var project = Project("Workspace");
        var task = CreateTask(project, "Retired task");
        repository.ProjectItems = [project];
        repository.TasksByProject[project.Id] = [task];
        var catalog = Catalog(repository);
        await catalog.ReloadAsync();
        var tabs = new WorkspaceConversationTabsViewModel(catalog);
        var oldTab = Assert.Single(tabs.Items);
        var lifetime = Assert.IsAssignableFrom<IDisposable>(tabs);
        lifetime.Dispose();
        lifetime.Dispose();
        catalog.StartNewTask();
        catalog.UpsertTask(task with { Title = "Updated after disposal" }, select: false);
        catalog.IsLocked = true;
        catalog.IsLocked = false;
        Assert.Empty(tabs.Items);
        Assert.Null(tabs.SelectedTab);
        Assert.False(tabs.SelectTabCommand.CanExecute(oldTab));
        Assert.False(tabs.CloseTabCommand.CanExecute(oldTab));
        tabs.SelectTabCommand.Execute(oldTab);
        tabs.CloseTabCommand.Execute(oldTab);
        Assert.Null(catalog.SelectedTask);
        Assert.Empty(repository.ArchiveOperations);
    }

    private static ProjectProfile Project(string name) => new(
        Guid.NewGuid(), name, "D:\\Dev\\MossAgent", ["D:\\Dev\\MossAgent"], DateTimeOffset.UtcNow);

    private static AgentTask CreateTask(ProjectProfile project, string title) => new(
        Guid.NewGuid(), project.Id, title, ApprovalPolicy.AskEveryTime,
        AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
}
