using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceRecentTasksViewModelTests
{
    [Fact]
    public void SetTasks_SortsAcrossProjectsAndFiltersTitles()
    {
        var older = CreateTask(Guid.NewGuid(), "旧会话", 1);
        var newer = CreateTask(Guid.NewGuid(), "最近的跨项目会话", 2);
        var recentTasks = new WorkspaceRecentTasksViewModel();

        recentTasks.SetTasks([older, newer], string.Empty);

        Assert.Equal(newer.Id, recentTasks.Items[0].Task.Id);
        recentTasks.ApplyFilter("跨项目");
        Assert.Single(recentTasks.Items);
        Assert.Equal(newer.Id, recentTasks.Items[0].Task.Id);
    }

    [Fact]
    public void UpsertResortsAndRemoveClearsSelection()
    {
        var projectId = Guid.NewGuid();
        var oldTask = CreateTask(projectId, "旧会话", 1);
        var selectedTask = CreateTask(projectId, "原始标题", 2);
        var recentTasks = new WorkspaceRecentTasksViewModel();
        recentTasks.SetTasks([oldTask, selectedTask], string.Empty);

        var updatedTask = selectedTask with
        {
            Title = "更新后的标题",
            UpdatedAt = DateTimeOffset.UnixEpoch.AddMinutes(3)
        };
        recentTasks.Upsert(updatedTask);
        recentTasks.SetSelectedTask(updatedTask.Id);

        Assert.Equal(updatedTask.Id, recentTasks.Items[0].Task.Id);
        Assert.Single(recentTasks.Items, item => item.Task.Id == updatedTask.Id);
        Assert.True(recentTasks.Items[0].IsSelected);

        recentTasks.Remove(updatedTask.Id);

        Assert.Single(recentTasks.Items);
        Assert.False(recentTasks.Items[0].IsSelected);
    }

    [Fact]
    public void SelectingTaskRetainsConversationsFromEveryProject()
    {
        var selectedProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var selectedTask = CreateTask(selectedProjectId, "当前项目任务", 2);
        var otherTask = CreateTask(otherProjectId, "其他项目任务", 1);
        var recentTasks = new WorkspaceRecentTasksViewModel();
        recentTasks.SetTasks([selectedTask, otherTask], string.Empty);

        recentTasks.SetSelectedTask(selectedTask.Id);

        Assert.Equal([selectedTask.Id, otherTask.Id], recentTasks.Items.Select(row => row.Task.Id));
        Assert.Equal(selectedTask.Id, Assert.Single(recentTasks.Items, row => row.IsSelected).Task.Id);
        Assert.True(recentTasks.HasItems);

        recentTasks.SetSelectedTask(otherTask.Id);

        Assert.Equal([selectedTask.Id, otherTask.Id], recentTasks.Items.Select(row => row.Task.Id));
        Assert.Equal(otherTask.Id, Assert.Single(recentTasks.Items, row => row.IsSelected).Task.Id);

        recentTasks.SetTasks([], string.Empty);

        Assert.Empty(recentTasks.Items);
        Assert.False(recentTasks.HasItems);
    }

    [Fact]
    public void PinnedTasksDoNotRepeatInRecentSection()
    {
        var task = CreateTask(Guid.NewGuid(), "置顶任务", 1);
        var recentTasks = new WorkspaceRecentTasksViewModel();

        recentTasks.SetTasks([task], string.Empty);
        Assert.Single(recentTasks.Items);
        recentTasks.Upsert(task with { IsPinned = true });

        Assert.Empty(recentTasks.Items);
        Assert.False(recentTasks.HasItems);
    }

    [Fact]
    public void DuplicateTasks_KeepNewestSnapshotAndFirstSnapshotWhenTimestampsTie()
    {
        var older = CreateTask(Guid.NewGuid(), "older", 1);
        var newest = older with { Title = "newest", UpdatedAt = DateTimeOffset.UnixEpoch.AddMinutes(3) };
        var tied = newest with { Title = "tied" };
        var other = CreateTask(Guid.NewGuid(), "other", 3);
        var recentTasks = new WorkspaceRecentTasksViewModel();

        recentTasks.SetTasks([older, other, newest, tied], string.Empty);

        Assert.Equal([newest, other], recentTasks.Items.Select(item => item.Task));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Upsert_RetainsDescendingOrderAndPlacesUpdatesAfterTimestampTies(int updatedMinute)
    {
        var projectId = Guid.NewGuid();
        var first = CreateTask(projectId, "match first", 3);
        var second = CreateTask(projectId, "match second", 1);
        var third = CreateTask(projectId, "match third", 2);
        var updated = second with { UpdatedAt = DateTimeOffset.UnixEpoch.AddMinutes(updatedMinute) };
        var recentTasks = new WorkspaceRecentTasksViewModel();
        recentTasks.SetTasks([second, first, third], string.Empty);

        recentTasks.Upsert(updated);
        recentTasks.ApplyFilter(" match ");

        var expected = new[] { first, third, updated }.OrderByDescending(task => task.UpdatedAt);
        Assert.Equal(expected, recentTasks.Items.Select(row => row.Task));
    }

    [Fact]
    public void SetTasks_UsesNewestDuplicateBeforePinFilteringAndPreservesTieOrder()
    {
        var older = CreateTask(Guid.NewGuid(), "match older", 1);
        var pinned = older with { UpdatedAt = DateTimeOffset.UnixEpoch.AddMinutes(4), IsPinned = true };
        var first = CreateTask(Guid.NewGuid(), "match first", 3);
        var second = CreateTask(Guid.NewGuid(), "match second", 3);
        var recentTasks = new WorkspaceRecentTasksViewModel();

        recentTasks.SetTasks([older, second, first, pinned], "match");
        recentTasks.ApplyFilter(" match ");

        Assert.Equal([second, first], recentTasks.Items.Select(row => row.Task));
    }

    private static AgentTask CreateTask(Guid projectId, string title, int updatedMinute)
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        return new AgentTask(
            Guid.NewGuid(), projectId, title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, createdAt,
            createdAt.AddMinutes(updatedMinute), null);
    }
}
