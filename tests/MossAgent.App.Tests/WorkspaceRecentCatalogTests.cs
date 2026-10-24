using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceRecentCatalogTests
{
    [Fact]
    public async Task ReloadIncludesCurrentProjectWithoutRepeatingPinnedTasks()
    {
        var (catalog, _, first, _, task, other) = CreateCatalog();
        using var management = catalog.TaskManagement;

        await catalog.ReloadAsync();

        Assert.Equal(first.Id, catalog.SelectedProject?.Id);
        Assert.Equal([other.Id, task.Id], catalog.RecentTasks.Items.Select(row => row.Task.Id));
        Assert.True(catalog.RecentTasks.HasItems);
        Assert.DoesNotContain(catalog.RecentTasks.Items, row => row.Task.IsPinned);
        Assert.Single(catalog.TaskManagement.PinnedTasks.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectAndDraftNavigationRetainRecentRowsAndOrdering(bool startDraft)
    {
        var (catalog, _, first, second, _, _) = CreateCatalog();
        using var management = catalog.TaskManagement;
        await catalog.ReloadAsync();
        var original = catalog.RecentTasks.Items.ToArray();
        var notifications = 0;
        catalog.RecentTasks.Items.CollectionChanged += (_, _) => notifications++;

        catalog.SelectProjectCommand.Execute(second);
        if (startDraft) catalog.NewTaskCommand.Execute(null);
        catalog.SelectProjectCommand.Execute(first);
        if (startDraft) catalog.NewTaskCommand.Execute(null);

        Assert.Equal(2, catalog.RecentTasks.Items.Count);
        Assert.Equal(original, catalog.RecentTasks.Items);
        Assert.All(original, row => Assert.Contains(catalog.RecentTasks.Items, current => ReferenceEquals(row, current)));
        Assert.Equal(0, notifications);
        Assert.Equal(first.Id, catalog.SelectedProject?.Id);
        if (startDraft) Assert.Null(catalog.SelectedTask);
    }

    [Fact]
    public async Task OpeningRecentConversationKeepsItVisibleAndSelectsItsProjectRow()
    {
        var (catalog, _, _, second, task, other) = CreateCatalog();
        using var management = catalog.TaskManagement;
        await catalog.ReloadAsync();

        catalog.SelectTaskCommand.Execute(other);

        Assert.Equal(second.Id, catalog.SelectedProject?.Id);
        Assert.Equal(other.Id, catalog.SelectedTask?.Id);
        Assert.Equal([other.Id, task.Id], catalog.RecentTasks.Items.Select(row => row.Task.Id));
        Assert.Equal(other.Id, Assert.Single(catalog.RecentTasks.Items, row => row.IsSelected).Task.Id);
        var projectRows = catalog.ProjectNavigation.Single(group => group.Project.Id == second.Id).Tasks;
        Assert.Equal(other.Id, Assert.Single(projectRows, row => row.IsSelected).Task.Id);
    }

    [Theory]
    [InlineData("first", 1)]
    [InlineData("other", 1)]
    [InlineData("missing", 0)]
    public async Task SearchUsesTitlesRatherThanCurrentProject(string query, int matches)
    {
        var (catalog, _, _, second, _, _) = CreateCatalog();
        using var management = catalog.TaskManagement;
        await catalog.ReloadAsync();
        catalog.TaskSearch.Query = query;
        var expected = catalog.RecentTasks.Items.ToArray();

        catalog.SelectProjectCommand.Execute(second);

        Assert.Equal(matches, expected.Length);
        Assert.Equal(expected, catalog.RecentTasks.Items);
        Assert.Equal(matches > 0, catalog.RecentTasks.HasItems);
        Assert.All(expected, row => Assert.Contains(query, row.Task.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectedPinPersistenceDoesNotHideCurrentProjectConversation()
    {
        var (catalog, repository, _, _, task, _) = CreateCatalog();
        using var management = catalog.TaskManagement;
        repository.RejectPinChange = true;
        await catalog.ReloadAsync();
        var original = catalog.RecentTasks.Items.ToArray();

        await management.TogglePinCommand.ExecuteAsync(task);

        Assert.Equal(original, catalog.RecentTasks.Items);
        Assert.Contains(catalog.RecentTasks.Items, row => row.Task.Id == task.Id && !row.Task.IsPinned);
        Assert.True(management.HasStatusError);
        Assert.False(management.PinnedTasks.IsPinned(task.Id));
        Assert.Equal((task.Id, true), Assert.Single(repository.PinOperations));
    }

    [Fact]
    public async Task PinUnpinArchiveAndRestoreUpdateBothCurrentProjectAndRecent()
    {
        var (catalog, repository, first, _, task, other) = CreateCatalog();
        using var management = catalog.TaskManagement;
        await catalog.ReloadAsync();

        await management.TogglePinCommand.ExecuteAsync(task);
        Assert.Equal(other.Id, Assert.Single(catalog.RecentTasks.Items).Task.Id);
        var pinned = management.PinnedTasks.Items.Single(row => row.Task.Id == task.Id).Task;
        await management.TogglePinCommand.ExecuteAsync(pinned);
        Assert.Contains(catalog.RecentTasks.Items, row => row.Task.Id == task.Id && !row.Task.IsPinned);

        var current = catalog.RecentTasks.Items.Single(row => row.Task.Id == task.Id).Task;
        await management.ArchiveCommand.ExecuteAsync(current);
        Assert.Equal(other.Id, Assert.Single(catalog.RecentTasks.Items).Task.Id);
        Assert.DoesNotContain(catalog.ProjectNavigation.Single(group => group.Project.Id == first.Id).Tasks,
            row => row.Task.Id == task.Id);
        management.SelectedArchivedTask = Assert.Single(management.ArchivedTasks);
        await management.RestoreCommand.ExecuteAsync(null);

        Assert.Equal([other.Id, task.Id], catalog.RecentTasks.Items.Select(row => row.Task.Id));
        Assert.Equal(task.Id, Assert.Single(catalog.RecentTasks.Items, row => row.IsSelected).Task.Id);
        Assert.Single(catalog.ProjectNavigation.Single(group => group.Project.Id == first.Id).Tasks,
            row => row.Task.Id == task.Id);
        Assert.Equal([(task.Id, true), (task.Id, false)], repository.ArchiveOperations);
    }

    [Fact]
    public async Task LockedNavigationDoesNotChangeRecentProjectionOrSelection()
    {
        var (catalog, repository, first, second, task, other) = CreateCatalog();
        using var management = catalog.TaskManagement;
        await catalog.ReloadAsync();
        var original = catalog.RecentTasks.Items.ToArray();
        catalog.IsLocked = true;

        catalog.SelectTaskCommand.Execute(other);
        catalog.SelectProjectCommand.Execute(second);
        catalog.NewTaskCommand.Execute(null);

        Assert.Equal(original, catalog.RecentTasks.Items);
        Assert.Equal(first.Id, catalog.SelectedProject?.Id);
        Assert.Equal(task.Id, catalog.SelectedTask?.Id);
        Assert.Empty(repository.PinOperations);
        Assert.Empty(repository.ArchiveOperations);
    }

    private static (WorkspaceCatalogViewModel Catalog, TestWorkspaceRepository Repository,
        ProjectProfile First, ProjectProfile Second, AgentTask Task, AgentTask Other) CreateCatalog()
    {
        var first = new ProjectProfile(Guid.NewGuid(), "first", "D:\\synthetic\\first", [], DateTimeOffset.UnixEpoch);
        var second = first with { Id = Guid.NewGuid(), Name = "other", PrimaryDirectory = "D:\\synthetic\\other" };
        var task = new AgentTask(Guid.NewGuid(), first.Id, "first task", ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), null);
        var other = task with { Id = Guid.NewGuid(), ProjectId = second.Id, Title = "other task", UpdatedAt = task.UpdatedAt.AddMinutes(1) };
        var pinned = task with { Id = Guid.NewGuid(), Title = "pinned task", IsPinned = true };
        var repository = new TestWorkspaceRepository { ProjectItems = [first, second], PinnedTasks = [pinned] };
        repository.TasksByProject[first.Id] = [task, pinned];
        repository.TasksByProject[second.Id] = [other];
        var catalog = new WorkspaceCatalogViewModel(repository, null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        return (catalog, repository, first, second, task, other);
    }
}
