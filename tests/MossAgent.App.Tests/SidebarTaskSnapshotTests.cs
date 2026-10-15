using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarTaskSnapshotTests
{
    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void EquivalentSnapshot_PreservesRowsWithoutCollectionNotifications(string kind)
    {
        var task = CreateTask();
        var (rows, setTasks, filter, _) = CreateList(kind, task.ProjectId);
        setTasks([task]);
        var original = Assert.Single(rows);
        var notifications = 0;
        rows.CollectionChanged += (_, _) => notifications++;

        filter("  task  ");
        setTasks([task with { }]);

        Assert.Same(original, Assert.Single(rows));
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void ChangedSnapshot_ReplacesChangedRowWithoutStaleTaskState(string kind)
    {
        var task = CreateTask();
        var unchanged = CreateTask() with { ProjectId = task.ProjectId };
        var (rows, setTasks, _, select) = CreateList(kind, task.ProjectId);
        setTasks([task, unchanged]);
        select(task.Id);
        var original = rows[0];
        var retained = rows[1];
        var changed = task with { Title = "task changed", Status = AgentTaskStatus.Running };

        setTasks([changed, unchanged]);

        Assert.NotSame(original, rows[0]);
        Assert.Equal(changed with { IsPinned = kind == "pinned" }, rows[0].Task);
        Assert.True(rows[0].IsSelected);
        Assert.True(rows[0].IsRunning);
        Assert.Same(retained, rows[1]);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Selection_UpdatesRetainedRowsWithoutResettingCollection(string kind)
    {
        var task = CreateTask();
        var (rows, setTasks, filter, select) = CreateList(kind, task.ProjectId);
        setTasks([task]);
        var original = Assert.Single(rows);
        var notifications = 0;
        rows.CollectionChanged += (_, _) => notifications++;

        select(task.Id);
        filter("task");
        Assert.True(original.IsSelected);
        select(null);
        filter(string.Empty);

        Assert.Same(original, Assert.Single(rows));
        Assert.False(original.IsSelected);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Filtering_RemovesRowsAndRestoresOnlyCurrentTasks(string kind)
    {
        var task = CreateTask();
        var (rows, setTasks, filter, select) = CreateList(kind, task.ProjectId);
        setTasks([task]);
        select(task.Id);

        filter("missing");
        Assert.Empty(rows);
        filter(string.Empty);
        Assert.True(Assert.Single(rows).IsSelected);
        setTasks([]);
        filter(string.Empty);

        Assert.Empty(rows);
    }

    [Fact]
    public void ProjectDuplicateSnapshot_KeepsIndependentRowInstances()
    {
        var task = CreateTask();
        var (rows, setTasks, _, _) = CreateList("project", task.ProjectId);

        setTasks([task, task]);
        var first = rows[0];
        var second = rows[1];
        setTasks([task, task]);

        Assert.NotSame(first, second);
        Assert.Same(first, rows[0]);
        Assert.Same(second, rows[1]);
    }

    private static (ObservableCollection<WorkspaceTaskNavigationViewModel> Rows,
        Action<IReadOnlyList<AgentTask>> SetTasks, Action<string> Filter, Action<Guid?> Select)
        CreateList(string kind, Guid? projectId)
    {
        if (kind == "pinned")
        {
            var model = new WorkspacePinnedTasksViewModel();
            return (model.Items, tasks => model.SetTasks(tasks, string.Empty), model.ApplyFilter, model.SetSelectedTask);
        }
        if (kind == "recent")
        {
            var model = new WorkspaceRecentTasksViewModel();
            return (model.Items, tasks => model.SetTasks(tasks.Select(task => task with { IsPinned = false }), string.Empty),
                model.ApplyFilter, model.SetSelectedTask);
        }
        var project = new ProjectProfile(Assert.IsType<Guid>(projectId), "project", "D:\\synthetic", [], DateTimeOffset.UnixEpoch);
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        return (navigation.Tasks, tasks => navigation.SetTasks(tasks.Select(task => task with { IsPinned = false })),
            navigation.ApplyFilter, navigation.SetSelectedTask);
    }

    private static AgentTask CreateTask() => new(Guid.NewGuid(), Guid.NewGuid(), "task",
        ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch, null, IsPinned: true);
}
