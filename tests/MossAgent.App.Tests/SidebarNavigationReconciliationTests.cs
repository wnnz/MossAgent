using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarNavigationReconciliationTests
{
    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Reordering_PreservesRowsAndPublishesOnlyMove(string kind)
    {
        var first = CreateTask("first");
        var second = CreateTask("second") with { ProjectId = first.ProjectId };
        var (rows, setTasks, _, select) = CreateList(kind, first.ProjectId);
        setTasks([first, second]);
        select(first.Id);
        var firstRow = rows[0];
        var secondRow = rows[1];
        var actions = Observe(rows);

        setTasks([second, first]);

        Assert.Same(secondRow, rows[0]);
        Assert.Same(firstRow, rows[1]);
        Assert.True(firstRow.IsSelected);
        Assert.False(secondRow.IsSelected);
        Assert.Equal([NotifyCollectionChangedAction.Move], actions);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Insertion_PreservesExistingRowsAndPublishesOnlyAdd(string kind)
    {
        var first = CreateTask("first");
        var inserted = CreateTask("inserted") with { ProjectId = first.ProjectId };
        var (rows, setTasks, _, _) = CreateList(kind, first.ProjectId);
        setTasks([first]);
        var original = Assert.Single(rows);
        var actions = Observe(rows);

        setTasks([inserted, first]);

        Assert.Equal(inserted.Id, rows[0].Task.Id);
        Assert.Same(original, rows[1]);
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Removal_PreservesRemainingRowsAndPublishesOnlyRemove(string kind)
    {
        var first = CreateTask("first");
        var second = CreateTask("second") with { ProjectId = first.ProjectId };
        var (rows, setTasks, _, _) = CreateList(kind, first.ProjectId);
        setTasks([first, second]);
        var retained = rows[1];
        var actions = Observe(rows);

        setTasks([second]);

        Assert.Same(retained, Assert.Single(rows));
        Assert.Equal([NotifyCollectionChangedAction.Remove], actions);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Filtering_PreservesMatchingRowWithoutReset(string kind)
    {
        var first = CreateTask("first");
        var second = CreateTask("second") with { ProjectId = first.ProjectId };
        var (rows, setTasks, filter, select) = CreateList(kind, first.ProjectId);
        setTasks([first, second]);
        select(second.Id);
        var retained = rows[1];
        var actions = Observe(rows);

        filter("second");

        Assert.Same(retained, Assert.Single(rows));
        Assert.True(retained.IsSelected);
        Assert.Equal([NotifyCollectionChangedAction.Remove], actions);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void MetadataChange_ReplacesOnlyChangedRowAndKeepsAuthoritativeState(string kind)
    {
        var first = CreateTask("first");
        var second = CreateTask("second") with { ProjectId = first.ProjectId };
        var (rows, setTasks, _, select) = CreateList(kind, first.ProjectId);
        setTasks([first, second]);
        select(first.Id);
        var replaced = rows[0];
        var retained = rows[1];
        var actions = Observe(rows);
        var updated = first with { Title = "updated", Status = AgentTaskStatus.Running };

        setTasks([updated, second]);

        Assert.NotSame(replaced, rows[0]);
        Assert.Equal(updated with { IsPinned = kind == "pinned" }, rows[0].Task);
        Assert.True(rows[0].IsSelected);
        Assert.True(rows[0].IsRunning);
        Assert.Same(retained, rows[1]);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("recent")]
    [InlineData("project")]
    public void Clearing_RemovesRowsWithoutResetOrRestoringRetiredTasks(string kind)
    {
        var task = CreateTask("task");
        var (rows, setTasks, filter, _) = CreateList(kind, task.ProjectId);
        setTasks([task]);
        var actions = Observe(rows);

        setTasks([]);
        filter(string.Empty);

        Assert.Empty(rows);
        Assert.Equal([NotifyCollectionChangedAction.Remove], actions);
    }

    [Fact]
    public void DuplicateTasks_RemainDistinctAndRetainOccurrenceOrderAfterMoves()
    {
        var task = CreateTask("task");
        var other = CreateTask("other") with { ProjectId = task.ProjectId };
        var (rows, setTasks, _, _) = CreateList("project", task.ProjectId);
        setTasks([task, task, other]);
        var first = rows[0];
        var second = rows[1];
        var moved = rows[2];
        var actions = Observe(rows);

        setTasks([other, task, task]);

        Assert.Same(moved, rows[0]);
        Assert.Same(first, rows[1]);
        Assert.Same(second, rows[2]);
        Assert.NotSame(first, second);
        Assert.Equal([NotifyCollectionChangedAction.Move], actions);
    }

    private static List<NotifyCollectionChangedAction> Observe(
        ObservableCollection<WorkspaceTaskNavigationViewModel> rows)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, args) => actions.Add(args.Action);
        return actions;
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

    private static AgentTask CreateTask(string title) => new(Guid.NewGuid(), Guid.NewGuid(), title,
        ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch, null, IsPinned: true);
}
