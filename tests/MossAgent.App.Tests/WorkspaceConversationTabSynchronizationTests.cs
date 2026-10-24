using System.Reflection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceConversationTabSynchronizationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Synchronization_RetainsHiddenPinnedAndCrossProjectTabsWithoutCollectionChanges()
    {
        var firstProject = Project("first");
        var secondProject = Project("second");
        var first = CreateTask(firstProject, "first") with { IsPinned = true };
        var second = CreateTask(secondProject, "second");
        var repository = Repository([firstProject, secondProject], [first, second]);
        var catalog = Catalog(repository);
        await catalog.ReloadAsync();
        using var tabs = new WorkspaceConversationTabsViewModel(catalog);
        catalog.SelectTaskCommand.Execute(second);
        var original = tabs.Items.ToArray();
        var selected = tabs.SelectedTab;
        var notifications = 0;
        tabs.Items.CollectionChanged += (_, _) => notifications++;
        catalog.TaskSearch.Query = "missing";
        var updated = second with { Title = "updated" };

        catalog.UpsertTask(updated);

        Assert.Equal(original, tabs.Items);
        Assert.Equal(0, notifications);
        Assert.Same(selected, tabs.SelectedTab);
        Assert.Same(first, tabs.Items[0].Task);
        Assert.Same(updated, tabs.Items[1].Task);
        Assert.Equal("updated", tabs.Items[1].Title);
        Assert.False(tabs.Items[0].IsSelected);
        Assert.True(tabs.Items[1].IsSelected);
        Assert.All(catalog.ProjectNavigation, group => Assert.Empty(group.Tasks));
        Assert.Empty(repository.ArchiveOperations);
    }

    [Fact]
    public async Task Synchronization_DoesNotFallBackToADuplicateOrForeignProject()
    {
        var project = Project("first");
        var foreignProject = Project("foreign");
        var first = CreateTask(project, "first");
        var second = CreateTask(project, "second");
        var foreign = first with { ProjectId = foreignProject.Id, Title = "foreign" };
        var catalog = Catalog(Repository([project, foreignProject], [first, second, foreign]));
        await catalog.ReloadAsync();
        using var tabs = new WorkspaceConversationTabsViewModel(catalog);
        catalog.SelectTaskCommand.Execute(second);
        var duplicate = new WorkspaceProjectNavigationViewModel(project with { Name = "duplicate" });
        duplicate.SetTasks([first]);
        catalog.ProjectNavigation.Add(duplicate);
        catalog.ProjectNavigation[0].RemoveTask(first.Id);

        catalog.StartNewTask();

        Assert.Equal(second.Id, Assert.Single(tabs.Items).Task.Id);
        Assert.Null(tabs.SelectedTab);
        Assert.False(tabs.ShowTabStrip);
        Assert.False(tabs.Items[0].IsSelected);
        Assert.Same(first, duplicate.FindTask(first.Id));
    }

    [Fact]
    public async Task DuplicateTabs_SelectTheFirstOccurrenceAndClosingOneDoesNotRecreateIt()
    {
        var project = Project("first");
        var first = CreateTask(project, "first");
        var second = CreateTask(project, "second");
        var repository = Repository([project], [first, second]);
        var catalog = Catalog(repository);
        await catalog.ReloadAsync();
        using var tabs = new WorkspaceConversationTabsViewModel(catalog);
        var original = tabs.Items[0];
        var duplicate = new ConversationTabViewModel(first);
        tabs.Items.Add(duplicate);
        var updated = first with { Title = "updated" };

        catalog.UpsertTask(updated);

        Assert.Equal(2, tabs.Items.Count);
        Assert.Same(original, tabs.SelectedTab);
        Assert.All(tabs.Items, tab => Assert.True(tab.IsSelected));
        tabs.CloseTabCommand.Execute(original);
        Assert.Same(duplicate, Assert.Single(tabs.Items));
        Assert.False(tabs.SelectTabCommand.CanExecute(original));
        Assert.False(tabs.CloseTabCommand.CanExecute(original));
        catalog.SelectTaskCommand.Execute(second);
        tabs.CloseTabCommand.Execute(duplicate);
        catalog.UpsertTask(second with { Title = "second updated" });
        Assert.Equal(second.Id, Assert.Single(tabs.Items).Task.Id);
        Assert.Empty(repository.ArchiveOperations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public async Task WarmSynchronization_AllocatesOnlyBoundedNotificationOverhead(int tabCount)
    {
        var project = Project("first");
        var tasks = Enumerable.Range(0, tabCount).Select(index => CreateTask(project, $"task {index}")).ToArray();
        var catalog = Catalog(Repository([project], tasks));
        await catalog.ReloadAsync();
        using var tabs = new WorkspaceConversationTabsViewModel(catalog);
        for (var index = 1; index < tasks.Length; index++) catalog.SelectTaskCommand.Execute(tasks[index]);
        var original = tabs.Items.ToArray();
        var selected = tabs.SelectedTab;
        var synchronize = typeof(WorkspaceConversationTabsViewModel)
            .GetMethod("Synchronize", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Action>(tabs);
        var notifications = 0;
        tabs.Items.CollectionChanged += (_, _) => notifications++;
        for (var index = 0; index < 1_000; index++) synchronize();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1_000; index++) synchronize();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"1,000 synchronizations, tabs={tabCount}: {allocated} bytes");
        Assert.Equal(original, tabs.Items);
        Assert.Same(selected, tabs.SelectedTab);
        Assert.Equal(0, notifications);
        Assert.InRange(allocated, 0, 1_000L * 128);
    }

    private static TestWorkspaceRepository Repository(ProjectProfile[] projects, AgentTask[] tasks)
    {
        var repository = new TestWorkspaceRepository { ProjectItems = projects };
        foreach (var project in projects)
            repository.TasksByProject[project.Id] = tasks.Where(task => task.ProjectId == project.Id).ToArray();
        return repository;
    }

    private static WorkspaceCatalogViewModel Catalog(TestWorkspaceRepository repository) => new(
        repository, null,
        new WorkspaceProjectService(repository, new TestFolderPickerService()));

    private static ProjectProfile Project(string name) => new(
        Guid.NewGuid(), name, "D:\\synthetic", [], DateTimeOffset.UnixEpoch);

    private static AgentTask CreateTask(ProjectProfile project, string title) => new(
        Guid.NewGuid(), project.Id, title, ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
}
