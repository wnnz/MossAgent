using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePinnedTasksViewModelTests
{
    [Fact]
    public void PinnedNavigationFiltersAndTracksSelectionWithoutDuplicates()
    {
        var task = new AgentTask(
            Guid.NewGuid(), Guid.NewGuid(), "Review changes", ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, true);
        var navigation = new WorkspacePinnedTasksViewModel();

        navigation.SetTasks([task, task, task with { Id = Guid.NewGuid(), IsPinned = false }], "");
        Assert.Equal(task.Id, Assert.Single(navigation.Items).Task.Id);
        navigation.SetSelectedTask(task.Id);
        Assert.True(navigation.Items[0].IsSelected);

        navigation.ApplyFilter("other");
        Assert.Empty(navigation.Items);
        Assert.False(navigation.HasItems);
        navigation.ApplyFilter("Review");
        Assert.True(Assert.Single(navigation.Items).IsSelected);

        navigation.Upsert(task with { IsPinned = false });
        Assert.Empty(navigation.Items);
        Assert.False(navigation.IsPinned(task.Id));
    }
}
